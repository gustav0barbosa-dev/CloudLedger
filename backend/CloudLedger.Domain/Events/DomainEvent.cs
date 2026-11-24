namespace CloudLedger.Domain.Events;

/// <summary>
/// Evento de domínio publicado na fila SQS. Consumidores devem ser idempotentes (RN-012).
/// </summary>
public sealed record DomainEvent(
    Guid EventId,
    string Type,
    Guid UserId,
    Guid? ResourceId,
    DateTime OccurredAt,
    IReadOnlyDictionary<string, string?> Payload);
