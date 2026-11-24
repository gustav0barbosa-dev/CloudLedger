using CloudLedger.Domain.Audit;

namespace CloudLedger.Application.Common;

public static class AuditEventFactory
{
    public static AuditEvent Create(
        Guid userId,
        string action,
        string resourceType,
        Guid? resourceId,
        string? correlationId,
        DateTime occurredAt,
        IReadOnlyDictionary<string, string?>? metadata = null) =>
        new(Guid.NewGuid(), userId, action, resourceType, resourceId, occurredAt, correlationId, metadata ?? new Dictionary<string, string?>());

    public static AuditEvent Create(
        Guid userId,
        string action,
        string resourceType,
        Guid? resourceId,
        string? correlationId,
        DateTime occurredAt,
        params (string Key, string? Value)[] metadata)
    {
        var dictionary = metadata.ToDictionary(x => x.Key, x => x.Value);
        return Create(userId, action, resourceType, resourceId, correlationId, occurredAt, dictionary);
    }
}
