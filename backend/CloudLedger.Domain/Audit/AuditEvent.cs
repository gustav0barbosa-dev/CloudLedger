namespace CloudLedger.Domain.Audit;

/// <summary>
/// Evento de auditoria persistido no MongoDB. Contém apenas metadados:
/// nunca senhas, tokens ou o conteúdo financeiro completo.
/// </summary>
public sealed record AuditEvent(
    Guid Id,
    Guid UserId,
    string Action,
    string ResourceType,
    Guid? ResourceId,
    DateTime OccurredAt,
    string? CorrelationId,
    IReadOnlyDictionary<string, string?> Metadata);
