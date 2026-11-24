using System.Text.Json;
using CloudLedger.Application.Abstractions;
using CloudLedger.Application.Abstractions.Persistence;
using CloudLedger.Domain.Events;
using Microsoft.Extensions.DependencyInjection;

namespace CloudLedger.Worker.Workers;

/// <summary>
/// Publica mensagens pendentes do outbox transacional. Somente marca como
/// publicada após o envio bem-sucedido ao barramento (SQS).
/// </summary>
public sealed class OutboxDispatcher : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IServiceProvider _services;
    private readonly ILogger<OutboxDispatcher> _logger;

    public OutboxDispatcher(IServiceProvider services, ILogger<OutboxDispatcher> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OutboxDispatcher iniciado.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha no ciclo de publicação do outbox.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("OutboxDispatcher encerrado.");
    }

    private async Task DispatchBatchAsync(CancellationToken cancellationToken)
    {
        await using var scope = _services.CreateAsyncScope();

        var outbox = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
        var sender = scope.ServiceProvider.GetRequiredService<IDomainEventSender>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var messages = await outbox.GetUnpublishedAsync(20, cancellationToken);
        if (messages.Count == 0)
        {
            return;
        }

        foreach (var message in messages)
        {
            DomainEvent? domainEvent;
            try
            {
                domainEvent = JsonSerializer.Deserialize<DomainEvent>(message.Payload, JsonOptions);
            }
            catch (Exception ex)
            {
                await outbox.MarkFailedAsync(message.Id, ex.Message, clock.UtcNow, cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);
                _logger.LogWarning(ex, "Mensagem de outbox {OutboxId} com payload inválido.", message.Id);
                continue;
            }

            if (domainEvent is null)
            {
                await outbox.MarkFailedAsync(message.Id, "Payload nulo", clock.UtcNow, cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);
                continue;
            }

            try
            {
                await sender.SendAsync(domainEvent, cancellationToken);
                await outbox.MarkPublishedAsync(message.Id, clock.UtcNow, cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                _logger.LogInformation(
                    "Evento {EventType} ({EventId}) publicado do outbox.",
                    domainEvent.Type,
                    domainEvent.EventId);
            }
            catch (Exception ex)
            {
                await outbox.MarkFailedAsync(message.Id, ex.Message, clock.UtcNow, cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                _logger.LogWarning(
                    ex,
                    "Falha ao publicar evento {EventType} ({EventId}); nova tentativa agendada.",
                    domainEvent.Type,
                    domainEvent.EventId);
            }
        }
    }
}
