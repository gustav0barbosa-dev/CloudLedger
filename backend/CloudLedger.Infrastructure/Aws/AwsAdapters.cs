using System.Collections.Concurrent;
using System.Text.Json;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using CloudLedger.Application.Abstractions;
using CloudLedger.Application.Abstractions.Persistence;
using CloudLedger.Application.Common.Exceptions;
using CloudLedger.Application.Common.Options;
using CloudLedger.Domain.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CloudLedger.Infrastructure.Aws;

public sealed class SqsDomainEventSender : IDomainEventSender
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IAmazonSQS _sqs;
    private readonly AwsOptions _options;
    private readonly ILogger<SqsDomainEventSender> _logger;
    private readonly SemaphoreSlim _queueLock = new(1, 1);
    private string? _queueUrl;

    public SqsDomainEventSender(IAmazonSQS sqs, IOptions<AwsOptions> options, ILogger<SqsDomainEventSender> logger)
    {
        _sqs = sqs;
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(DomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        var queueUrl = await GetQueueUrlAsync(cancellationToken);
        var body = JsonSerializer.Serialize(domainEvent, JsonOptions);

        try
        {
            await _sqs.SendMessageAsync(
                new SendMessageRequest
                {
                    QueueUrl = queueUrl,
                    MessageBody = body,
                    MessageAttributes = new Dictionary<string, MessageAttributeValue>
                    {
                        ["eventType"] = new() { DataType = "String", StringValue = domainEvent.Type }
                    }
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            // Não engolir a falha: o outbox deve manter a mensagem pendente para nova tentativa.
            _logger.LogError(ex, "Falha ao enviar evento {EventType} para SQS", domainEvent.Type);
            throw;
        }
    }

    private async Task<string> GetQueueUrlAsync(CancellationToken cancellationToken)
    {
        if (_queueUrl is not null)
        {
            return _queueUrl;
        }

        await _queueLock.WaitAsync(cancellationToken);
        try
        {
            if (_queueUrl is not null)
            {
                return _queueUrl;
            }

            var dlqUrl = await EnsureDeadLetterQueueAsync(cancellationToken);

            string queueUrl;
            try
            {
                queueUrl = (await _sqs.GetQueueUrlAsync(_options.SqsQueueName, cancellationToken)).QueueUrl;
            }
            catch (QueueDoesNotExistException)
            {
                var created = await _sqs.CreateQueueAsync(
                    new CreateQueueRequest
                    {
                        QueueName = _options.SqsQueueName,
                        Attributes = new Dictionary<string, string>
                        {
                            ["VisibilityTimeout"] = "60",
                            ["MessageRetentionPeriod"] = "1209600"
                        }
                    },
                    cancellationToken);
                queueUrl = created.QueueUrl;
                _logger.LogInformation("Fila SQS {QueueName} criada em {QueueUrl}", _options.SqsQueueName, queueUrl);
            }

            var queueArn = await GetQueueArnAsync(queueUrl, cancellationToken);
            var dlqArn = await GetQueueArnAsync(dlqUrl, cancellationToken);
            var redrivePolicy = JsonSerializer.Serialize(new
            {
                deadLetterTargetArn = dlqArn,
                maxReceiveCount = "5"
            });

            await _sqs.SetQueueAttributesAsync(
                new SetQueueAttributesRequest
                {
                    QueueUrl = queueUrl,
                    Attributes = new Dictionary<string, string>
                    {
                        ["VisibilityTimeout"] = "60",
                        ["MessageRetentionPeriod"] = "1209600",
                        ["RedrivePolicy"] = redrivePolicy
                    }
                },
                cancellationToken);

            _queueUrl = queueUrl;
            return _queueUrl;
        }
        finally
        {
            _queueLock.Release();
        }
    }

    private async Task<string> EnsureDeadLetterQueueAsync(CancellationToken cancellationToken)
    {
        try
        {
            return (await _sqs.GetQueueUrlAsync(_options.SqsDeadLetterQueueName, cancellationToken)).QueueUrl;
        }
        catch (QueueDoesNotExistException)
        {
            var created = await _sqs.CreateQueueAsync(
                new CreateQueueRequest
                {
                    QueueName = _options.SqsDeadLetterQueueName,
                    Attributes = new Dictionary<string, string>
                    {
                        ["MessageRetentionPeriod"] = "1209600"
                    }
                },
                cancellationToken);
            _logger.LogInformation("DLQ SQS {QueueName} criada em {QueueUrl}", _options.SqsDeadLetterQueueName, created.QueueUrl);
            return created.QueueUrl;
        }
    }

    private async Task<string> GetQueueArnAsync(string queueUrl, CancellationToken cancellationToken)
    {
        var response = await _sqs.GetQueueAttributesAsync(
            new GetQueueAttributesRequest
            {
                QueueUrl = queueUrl,
                AttributeNames = new List<string> { "QueueArn" }
            },
            cancellationToken);

        return response.Attributes["QueueArn"];
    }
}

public sealed class LoggingDomainEventSender : IDomainEventSender
{
    private readonly ILogger<LoggingDomainEventSender> _logger;

    public LoggingDomainEventSender(ILogger<LoggingDomainEventSender> logger) => _logger = logger;

    public Task SendAsync(DomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Evento de domínio {EventType} ({EventId}) enviado pelo outbox em modo local (AWS desabilitada)",
            domainEvent.Type,
            domainEvent.EventId);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Publica eventos no outbox transacional. A gravação acontece no mesmo
/// SaveChanges da operação financeira que originou o evento.
/// </summary>
public sealed class OutboxDomainEventPublisher : IDomainEventPublisher
{
    private readonly IOutboxRepository _outbox;

    public OutboxDomainEventPublisher(IOutboxRepository outbox) => _outbox = outbox;

    public Task PublishAsync(DomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        _outbox.Add(new OutboxMessage(domainEvent));
        return Task.CompletedTask;
    }
}

public sealed class DynamoIdempotencyStore : IIdempotencyStore
{
    private static readonly TimeSpan StaleProcessingAfter = TimeSpan.FromMinutes(10);

    private readonly IAmazonDynamoDB _dynamo;
    private readonly AwsOptions _options;
    private readonly ILogger<DynamoIdempotencyStore> _logger;

    public DynamoIdempotencyStore(IAmazonDynamoDB dynamo, IOptions<AwsOptions> options, ILogger<DynamoIdempotencyStore> logger)
    {
        _dynamo = dynamo;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<bool> TryBeginAsync(string key, string requestHash, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var expiresAt = DateTimeOffset.UtcNow.Add(ttl).ToUnixTimeSeconds().ToString();

        var item = new Dictionary<string, AttributeValue>
        {
            ["PK"] = new() { S = key },
            ["RequestHash"] = new() { S = requestHash },
            ["Status"] = new() { S = "Processing" },
            ["StartedAt"] = new() { N = now.ToString() },
            ["ExpiresAt"] = new() { N = expiresAt }
        };

        try
        {
            await _dynamo.PutItemAsync(
                new PutItemRequest
                {
                    TableName = _options.DynamoTableName,
                    Item = item,
                    ConditionExpression = "attribute_not_exists(PK)"
                },
                cancellationToken);
            return true;
        }
        catch (ConditionalCheckFailedException)
        {
            var existing = await _dynamo.GetItemAsync(
                new GetItemRequest
                {
                    TableName = _options.DynamoTableName,
                    Key = new Dictionary<string, AttributeValue> { ["PK"] = new() { S = key } },
                    ConsistentRead = true
                },
                cancellationToken);

            if (!existing.Item.TryGetValue("RequestHash", out var hash)
                || !string.Equals(hash.S, requestHash, StringComparison.Ordinal))
            {
                throw new ConflictException("idempotency.conflict", "A mesma chave de idempotência foi usada com um conteúdo diferente.");
            }

            var status = existing.Item.TryGetValue("Status", out var statusAttribute)
                ? statusAttribute.S
                : "Processing";

            if (string.Equals(status, "Completed", StringComparison.Ordinal))
            {
                return false;
            }

            if (string.Equals(status, "Processing", StringComparison.Ordinal)
                && !IsStale(existing.Item))
            {
                return false;
            }

            // Registro abandonado (Processing) ou falho (Failed): recupera a reserva
            // com CONDITION para não disputar com outro processo.
            try
            {
                await _dynamo.UpdateItemAsync(
                    new UpdateItemRequest
                    {
                        TableName = _options.DynamoTableName,
                        Key = new Dictionary<string, AttributeValue> { ["PK"] = new() { S = key } },
                        UpdateExpression = "SET #status = :processing, StartedAt = :started, ExpiresAt = :expires REMOVE CompletedAt, FailedAt",
                        ConditionExpression = "#status = :currentStatus",
                        ExpressionAttributeNames = new Dictionary<string, string> { ["#status"] = "Status" },
                        ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                        {
                            [":processing"] = new() { S = "Processing" },
                            [":currentStatus"] = new() { S = status },
                            [":started"] = new() { N = now.ToString() },
                            [":expires"] = new() { N = expiresAt }
                        }
                    },
                    cancellationToken);
                return true;
            }
            catch (ConditionalCheckFailedException)
            {
                return false;
            }
        }
    }

    public async Task CompleteAsync(string key, string? resultReference, CancellationToken cancellationToken = default)
    {
        try
        {
            await _dynamo.UpdateItemAsync(
                new UpdateItemRequest
                {
                    TableName = _options.DynamoTableName,
                    Key = new Dictionary<string, AttributeValue> { ["PK"] = new() { S = key } },
                    UpdateExpression = "SET #status = :status, ResultReference = :reference, CompletedAt = :completed REMOVE FailedAt",
                    ExpressionAttributeNames = new Dictionary<string, string> { ["#status"] = "Status" },
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                    {
                        [":status"] = new() { S = "Completed" },
                        [":reference"] = new() { S = resultReference ?? string.Empty },
                        [":completed"] = new() { N = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString() }
                    }
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao concluir registro de idempotência {Key}", key);
        }
    }

    public async Task FailAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            await _dynamo.UpdateItemAsync(
                new UpdateItemRequest
                {
                    TableName = _options.DynamoTableName,
                    Key = new Dictionary<string, AttributeValue> { ["PK"] = new() { S = key } },
                    UpdateExpression = "SET #status = :status, FailedAt = :failed REMOVE CompletedAt",
                    ExpressionAttributeNames = new Dictionary<string, string> { ["#status"] = "Status" },
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                    {
                        [":status"] = new() { S = "Failed" },
                        [":failed"] = new() { N = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString() }
                    }
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao registrar falha de idempotência {Key}", key);
        }
    }

    private static bool IsStale(IReadOnlyDictionary<string, AttributeValue> item)
    {
        if (!item.TryGetValue("StartedAt", out var attribute)
            || !long.TryParse(attribute.N, out var seconds))
        {
            return true;
        }

        var startedAt = DateTimeOffset.FromUnixTimeSeconds(seconds);
        return DateTimeOffset.UtcNow - startedAt >= StaleProcessingAfter;
    }
}

public sealed class InMemoryIdempotencyStore : IIdempotencyStore
{
    private sealed record Entry(string RequestHash, DateTime ExpiresAtUtc);

    private readonly ConcurrentDictionary<string, Entry> _entries = new();

    public Task<bool> TryBeginAsync(string key, string requestHash, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        Cleanup();
        var now = DateTime.UtcNow;
        var entry = new Entry(requestHash, now.Add(ttl));

        if (_entries.TryAdd(key, entry))
        {
            return Task.FromResult(true);
        }

        var existing = _entries[key];
        if (existing.ExpiresAtUtc <= now)
        {
            _entries[key] = entry;
            return Task.FromResult(true);
        }

        if (!string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal))
        {
            throw new ConflictException("idempotency.conflict", "A mesma chave de idempotência foi usada com um conteúdo diferente.");
        }

        return Task.FromResult(false);
    }

    public Task CompleteAsync(string key, string? resultReference, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task FailAsync(string key, CancellationToken cancellationToken = default)
    {
        _entries.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    private void Cleanup()
    {
        var now = DateTime.UtcNow;
        foreach (var entry in _entries)
        {
            if (entry.Value.ExpiresAtUtc <= now)
            {
                _entries.TryRemove(entry.Key, out _);
            }
        }
    }
}

public sealed class S3ReportStorage : IReportStorage
{
    private readonly IAmazonS3 _s3;
    private readonly AwsOptions _options;

    public S3ReportStorage(IAmazonS3 s3, IOptions<AwsOptions> options)
    {
        _s3 = s3;
        _options = options.Value;
    }

    public async Task<string> SaveAsync(string fileName, string contentType, string content, CancellationToken cancellationToken = default)
    {
        var key = $"reports/{fileName}";

        await _s3.PutObjectAsync(
            new PutObjectRequest
            {
                BucketName = _options.S3BucketName,
                Key = key,
                ContentBody = content,
                ContentType = contentType
            },
            cancellationToken);

        return _s3.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = _options.S3BucketName,
            Key = key,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.AddMinutes(15)
        });
    }
}

public sealed class LocalReportStorage : IReportStorage
{
    public async Task<string> SaveAsync(string fileName, string contentType, string content, CancellationToken cancellationToken = default)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "reports");
        Directory.CreateDirectory(directory);

        var path = Path.Combine(directory, fileName);
        await File.WriteAllTextAsync(path, content, cancellationToken);

        return $"/api/v1/reports/files/{fileName}";
    }
}

public static class AwsClientFactory
{
    public static AmazonSQSConfig SqsConfig(AwsOptions options) => BaseConfig(new AmazonSQSConfig(), options);

    public static AmazonDynamoDBConfig DynamoConfig(AwsOptions options) => BaseConfig(new AmazonDynamoDBConfig(), options);

    public static AmazonS3Config S3Config(AwsOptions options)
    {
        var config = BaseConfig(new AmazonS3Config(), options);
        // LocalStack não publica DNS para buckets virtuais (bucket.localhost).
        if (options.UseLocalStack)
        {
            config.ForcePathStyle = true;
        }

        return config;
    }

    public static AWSCredentials? Credentials(AwsOptions options) =>
        options.UseLocalStack ? new BasicAWSCredentials("test", "test") : null;

    private static T BaseConfig<T>(T config, AwsOptions options) where T : Amazon.Runtime.ClientConfig
    {
        if (!string.IsNullOrWhiteSpace(options.ServiceUrl))
        {
            config.ServiceURL = options.ServiceUrl;
            config.AuthenticationRegion = options.Region;
        }
        else
        {
            config.RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(options.Region);
        }

        return config;
    }
}
