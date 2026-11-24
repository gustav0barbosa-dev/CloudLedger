using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.DependencyInjection;
using CloudLedger.Application.Common.Options;
using CloudLedger.Domain.Events;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace CloudLedger.Worker.Workers;

/// <summary>
/// Consumidor de eventos de domínio publicados na fila SQS.
/// Quando AWS está desabilitada, atua como heartbeat (sem consumo real).
/// </summary>
public sealed class SqsEventConsumer : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ILogger<SqsEventConsumer> _logger;
    private readonly IAmazonSQS? _sqs;
    private readonly AwsOptions _options;

    public SqsEventConsumer(ILogger<SqsEventConsumer> logger, IServiceProvider services, IOptions<AwsOptions> options)
    {
        _logger = logger;
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
                    try
                    {
                        var evt = JsonSerializer.Deserialize<DomainEvent>(message.Body, JsonOptions);
                        _logger.LogInformation("Evento consumido: {Type} (id={Id}) user={UserId}", evt?.Type, evt?.EventId, evt?.UserId);

                        await _sqs.DeleteMessageAsync(queueUrl, message.ReceiptHandle, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Falha ao processar mensagem SQS {MessageId}; será reprocessada.", message.MessageId);
                    }
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
}