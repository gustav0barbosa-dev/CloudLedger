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
using CloudLedger.Application.Common.Exceptions;
using CloudLedger.Application.Common.Options;
using CloudLedger.Domain.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CloudLedger.Infrastructure.Aws;

public sealed class SqsDomainEventPublisher : IDomainEventPublisher
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IAmazonSQS _sqs;
    private readonly AwsOptions _options;
    private readonly ILogger<SqsDomainEventPublisher> _logger;
    private readonly SemaphoreSlim _queueLock = new(1, 1);
    private string? _queueUrl;

    public SqsDomainEventPublisher(IAmazonSQS sqs, IOptions<AwsOptions> options, ILogger<SqsDomainEventPublisher> logger)
    {
        _sqs = sqs;
        _options = options.Value;
        _logger = logger;
    }

    public async Task PublishAsync(DomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            var queueUrl = await GetQueueUrlAsync(cancellationToken);
            var body = JsonSerializer.Serialize(domainEvent, JsonOptions);

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
            _logger.LogWarning(ex, "Falha ao publicar evento {EventType} na fila SQS", domainEvent.Type);
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

            try
            {
                var response = await _sqs.GetQueueUrlAsync(_options.SqsQueueName, cancellationToken);
                _queueUrl = response.QueueUrl;
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
                _queueUrl = created.QueueUrl;
                _logger.LogInformation("Fila SQS {QueueName} criada em {QueueUrl}", _options.SqsQueueName, _queueUrl);
            }

            return _queueUrl;
        }
        finally
        {
            _queueLock.Release();
        }
    }
}

public sealed class LoggingDomainEventPublisher : IDomainEventPublisher
{
    private readonly ILogger<LoggingDomainEventPublisher> _logger;

    public LoggingDomainEventPublisher(ILogger<LoggingDomainEventPublisher> logger) => _logger = logger;

    public Task PublishAsync(DomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Evento de domínio {EventType} ({EventId}) para usuário {UserId} — AWS desabilitada, evento não enfileirado",
            domainEvent.Type,
            domainEvent.EventId,
            domainEvent.UserId);
        return Task.CompletedTask;
    }
}

public sealed class DynamoIdempotencyStore : IIdempotencyStore
{
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
        var item = new Dictionary<string, AttributeValue>
        {
            ["PK"] = new() { S = key },
            ["RequestHash"] = new() { S = requestHash },
            ["Status"] = new() { S = "Processing" },
            ["ExpiresAt"] = new() { N = DateTimeOffset.UtcNow.Add(ttl).ToUnixTimeSeconds().ToString() }
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

            if (existing.Item.TryGetValue("RequestHash", out var hash)
                && string.Equals(hash.S, requestHash, StringComparison.Ordinal))
            {
                return false;
            }

            throw new ConflictException("idempotency.conflict", "A mesma chave de idempotência foi usada com um conteúdo diferente.");
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
                    UpdateExpression = "SET #status = :status, ResultReference = :reference",
                    ExpressionAttributeNames = new Dictionary<string, string> { ["#status"] = "Status" },
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                    {
                        [":status"] = new() { S = "Completed" },
                        [":reference"] = new() { S = resultReference ?? string.Empty }
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
            await _dynamo.DeleteItemAsync(
                new DeleteItemRequest
                {
                    TableName = _options.DynamoTableName,
                    Key = new Dictionary<string, AttributeValue> { ["PK"] = new() { S = key } }
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao liberar registro de idempotência {Key}", key);
        }
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

        return $"/reports/{fileName}";
    }
}

public static class AwsClientFactory
{
    public static AmazonSQSConfig SqsConfig(AwsOptions options) => BaseConfig(new AmazonSQSConfig(), options);

    public static AmazonDynamoDBConfig DynamoConfig(AwsOptions options) => BaseConfig(new AmazonDynamoDBConfig(), options);

    public static AmazonS3Config S3Config(AwsOptions options) => BaseConfig(new AmazonS3Config(), options);

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
