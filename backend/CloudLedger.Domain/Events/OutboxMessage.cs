using System.Text.Json;
using CloudLedger.Domain.Common;

namespace CloudLedger.Domain.Events;

/// <summary>
/// Mensagem de outbox (Transactional Outbox pattern): persistida na mesma
/// transação da operação financeira e publicada assincronamente pelo Worker.
/// </summary>
public sealed class OutboxMessage : Entity
{
    private OutboxMessage() { }

    public OutboxMessage(DomainEvent domainEvent)
    {
        Id = domainEvent.EventId;
        Type = domainEvent.Type;
        UserId = domainEvent.UserId;
        ResourceId = domainEvent.ResourceId;
        OccurredAt = domainEvent.OccurredAt;
        Payload = JsonSerializer.Serialize(domainEvent, JsonOptions);
        Attempts = 0;
    }

    public string Type { get; private set; } = string.Empty;
    public Guid UserId { get; private set; }
    public Guid? ResourceId { get; private set; }
    public DateTime OccurredAt { get; private set; }
    public string Payload { get; private set; } = string.Empty;
    public DateTime? PublishedAt { get; private set; }
    public DateTime? NextAttemptAtUtc { get; private set; }
    public int Attempts { get; private set; }
    public string? LastError { get; private set; }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public bool IsPublished => PublishedAt is not null;

    public void MarkPublished(DateTime utcNow)
    {
        PublishedAt = utcNow;
        NextAttemptAtUtc = null;
        LastError = null;
    }

    public void MarkFailed(string error, DateTime utcNow)
    {
        Attempts++;
        LastError = error.Length > 500 ? error[..500] : error;

        var seconds = Math.Min(Math.Pow(2, Math.Min(Attempts, 6)), 60);
        NextAttemptAtUtc = utcNow.AddSeconds(seconds);
    }
}
