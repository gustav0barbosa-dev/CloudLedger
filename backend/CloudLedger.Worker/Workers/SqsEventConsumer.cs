using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using CloudLedger.Application.Abstractions;
using CloudLedger.Application.Common.Options;
using CloudLedger.Domain.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CloudLedger.Worker.Workers;

/// <summary>
/// Consumidor de eventos de domínio publicados na fila SQS.
/// Processa o evento de negócio antes de excluir a mensagem e usa
/// idempotência por EventId para evitar duplicidade.
/// </summary>
public sealed class SqsEventConsumer : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ILogger<SqsEventConsumer> _logger;
    private readonly IServiceProvider _services;
    private readonly IAmazonSQS? _sqs;
    private readonly AwsOptions _options;

    public SqsEventConsumer(
        ILogger<SqsEventConsumer> logger,
        IServiceProvider services,
        IOptions<AwsOptions> options)
    {
        _logger = logger;
        _services = services;
        _sqs = services.GetService<IAmazonSQS>();
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled || _sqs is null)
        {
            _logger.LogInformation("CloudLedger.Worker em modo local — consumidor SQS desabilitado.");
            while (!stoppingToken.IsCancellationRequested)
            {
                try { await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
            return;
        }

        string? queueUrl = null;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                queueUrl ??= (await _sqs.GetQueueUrlAsync(_options.SqsQueueName, stoppingToken)).QueueUrl;

                var response = await _sqs.ReceiveMessageAsync(new ReceiveMessageRequest
                {
                    QueueUrl = queueUrl,
                    MaxNumberOfMessages = 10,
                    WaitTimeSeconds = 20,
                    VisibilityTimeout = 60,
                    MessageAttributeNames = new List<string> { "All" }
                }, stoppingToken);

                if (response.Messages is null || response.Messages.Count == 0)
                {
                    continue;
                }

                foreach (var message in response.Messages)
                {
                    await ProcessMessageAsync(queueUrl, message, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Erro no loop de consumo SQS; aguardando antes de tentar novamente.");
                try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }

        _logger.LogInformation("CloudLedger.Worker encerrado.");
    }

    private async Task ProcessMessageAsync(string queueUrl, Message message, CancellationToken cancellationToken)
    {
        DomainEvent? domainEvent;
        try
        {
            domainEvent = JsonSerializer.Deserialize<DomainEvent>(message.Body, JsonOptions);
        }
        catch (Exception ex)
        {
            // Mensagem inválida permanece na fila e será direcionada à DLQ.
            _logger.LogWarning(ex, "Payload inválido na mensagem SQS {MessageId}.", message.MessageId);
            return;
        }

        if (domainEvent is null)
        {
            _logger.LogWarning("Mensagem SQS {MessageId} com payload vazio.", message.MessageId);
            return;
        }

        var key = $"sqs-event:{domainEvent.EventId}";
        var requestHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(message.Body)));

        await using var scope = _services.CreateAsyncScope();
        var idempotency = scope.ServiceProvider.GetRequiredService<IIdempotencyStore>();
        var processor = scope.ServiceProvider.GetRequiredService<IDomainEventProcessor>();

        try
        {
            var shouldProcess = await idempotency.TryBeginAsync(
                key,
                requestHash,
                TimeSpan.FromHours(24),
                cancellationToken);

            if (!shouldProcess)
            {
                _logger.LogInformation(
                    "Evento {EventId} já processado; mensagem {MessageId} será descartada.",
                    domainEvent.EventId,
                    message.MessageId);
                await DeleteAsync(queueUrl, message, cancellationToken);
                return;
            }

            await processor.ProcessAsync(domainEvent, cancellationToken);
            await idempotency.CompleteAsync(key, domainEvent.EventId.ToString(), cancellationToken);
            await DeleteAsync(queueUrl, message, cancellationToken);

            _logger.LogInformation(
                "Evento {EventType} ({EventId}) processado e mensagem {MessageId} excluída.",
                domainEvent.Type,
                domainEvent.EventId,
                message.MessageId);
        }
        catch (Exception ex)
        {
            await idempotency.FailAsync(key, cancellationToken);
            _logger.LogWarning(
                ex,
                "Falha ao processar evento {EventId}; mensagem {MessageId} será reprocessada.",
                domainEvent.EventId,
                message.MessageId);
        }
    }

    private async Task DeleteAsync(string queueUrl, Message message, CancellationToken cancellationToken)
    {
        await _sqs!.DeleteMessageAsync(queueUrl, message.ReceiptHandle, cancellationToken);
    }
}
