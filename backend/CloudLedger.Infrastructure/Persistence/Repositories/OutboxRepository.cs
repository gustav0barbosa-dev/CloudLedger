using CloudLedger.Application.Abstractions.Persistence;
using CloudLedger.Domain.Events;
using Microsoft.EntityFrameworkCore;

namespace CloudLedger.Infrastructure.Persistence.Repositories;

public sealed class OutboxRepository : IOutboxRepository
{
    private readonly AppDbContext _db;

    public OutboxRepository(AppDbContext db) => _db = db;

    public void Add(OutboxMessage message) => _db.OutboxMessages.Add(message);

    public async Task<IReadOnlyList<OutboxMessage>> GetUnpublishedAsync(
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        return await _db.OutboxMessages
            .AsNoTracking()
            .Where(x => x.PublishedAt == null
                        && (x.NextAttemptAtUtc == null || x.NextAttemptAtUtc <= now))
            .OrderBy(x => x.OccurredAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }

    public async Task MarkPublishedAsync(Guid id, DateTime utcNow, CancellationToken cancellationToken = default)
    {
        var message = await _db.OutboxMessages.FindAsync([id], cancellationToken);
        message?.MarkPublished(utcNow);
    }

    public async Task MarkFailedAsync(Guid id, string error, DateTime utcNow, CancellationToken cancellationToken = default)
    {
        var message = await _db.OutboxMessages.FindAsync([id], cancellationToken);
        message?.MarkFailed(error, utcNow);
    }
}
