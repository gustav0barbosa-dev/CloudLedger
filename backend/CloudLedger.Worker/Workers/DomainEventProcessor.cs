using CloudLedger.Application.Abstractions;
using CloudLedger.Application.Abstractions.Persistence;
using CloudLedger.Domain.Audit;
using CloudLedger.Domain.Events;
using CloudLedger.Domain.Notifications;

namespace CloudLedger.Worker.Workers;

public interface IDomainEventProcessor
{
    Task ProcessAsync(DomainEvent domainEvent, CancellationToken cancellationToken = default);
}

/// <summary>
/// Processamento de negócio dos eventos consumidos da fila. Cria notificações
/// deduplicadas para os eventos financeiros suportados.
/// </summary>
public sealed class DomainEventProcessor : IDomainEventProcessor
{
    private static readonly HashSet<string> SupportedTypes = new(StringComparer.Ordinal)
    {
        AuditActions.TransactionCreated,
        AuditActions.TransactionVoided,
        AuditActions.TransferCreated,
        AuditActions.TransferVoided,
        AuditActions.RecurringOccurrencePaid,
        AuditActions.GoalMovementCreated
    };

    private readonly INotificationRepository _notifications;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ILogger<DomainEventProcessor> _logger;

    public DomainEventProcessor(
        INotificationRepository notifications,
        IUnitOfWork unitOfWork,
        IClock clock,
        ILogger<DomainEventProcessor> logger)
    {
        _notifications = notifications;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _logger = logger;
    }

    public async Task ProcessAsync(DomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        if (!SupportedTypes.Contains(domainEvent.Type))
        {
            _logger.LogInformation("Evento {EventType} sem handler de negócio; ignorado.", domainEvent.Type);
            return;
        }

        var resourceId = domainEvent.ResourceId ?? domainEvent.EventId;
        var sinceUtc = _clock.UtcNow.AddMinutes(-5);

        if (await _notifications.ExistsRecentAsync(
                domainEvent.UserId,
                NotificationType.Info,
                resourceId,
                sinceUtc,
                cancellationToken))
        {
            _logger.LogInformation(
                "Notificação para o evento {EventId} já existe; processamento ignorado.",
                domainEvent.EventId);
            return;
        }

        var (title, message) = Describe(domainEvent);

        _notifications.Add(new Notification(
            domainEvent.UserId,
            title,
            message,
            NotificationType.Info,
            "DomainEvent",
            resourceId));

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Evento {EventType} processado; notificação criada para {UserId}.",
            domainEvent.Type,
            domainEvent.UserId);
    }

    private static (string Title, string Message) Describe(DomainEvent domainEvent)
    {
        var amount = domainEvent.Payload.TryGetValue("amount", out var value)
                     && !string.IsNullOrWhiteSpace(value)
            ? value
            : "—";

        return domainEvent.Type switch
        {
            AuditActions.TransactionCreated => ("Lançamento registrado", $"Um lançamento de R$ {amount} foi registrado."),
            AuditActions.TransactionVoided => ("Lançamento cancelado", $"O lançamento de R$ {amount} foi cancelado."),
            AuditActions.TransferCreated => ("Transferência registrada", $"Uma transferência de R$ {amount} foi registrada."),
            AuditActions.TransferVoided => ("Transferência cancelada", $"A transferência de R$ {amount} foi cancelada."),
            AuditActions.RecurringOccurrencePaid => ("Pagamento recorrente registrado", $"Um pagamento recorrente de R$ {amount} foi registrado."),
            AuditActions.GoalMovementCreated => ("Movimento de meta registrado", $"Um movimento de R$ {amount} foi registrado em uma meta."),
            _ => ("Evento financeiro", "Um evento financeiro foi processado.")
        };
    }
}
