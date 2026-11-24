using CloudLedger.Application.Abstractions.Persistence;
using CloudLedger.Application.Common.Models;
using CloudLedger.Domain.Goals;
using CloudLedger.Domain.Notifications;
using Microsoft.EntityFrameworkCore;

namespace CloudLedger.Infrastructure.Persistence.Repositories;

public sealed class SavingGoalRepository : ISavingGoalRepository
{
    private readonly AppDbContext _db;

    public SavingGoalRepository(AppDbContext db) => _db = db;

    public Task<SavingGoal?> GetAsync(Guid userId, Guid id, CancellationToken cancellationToken = default) =>
        _db.SavingGoals.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, cancellationToken);

    public async Task<IReadOnlyList<SavingGoal>> ListAsync(Guid userId, GoalStatus? status, CancellationToken cancellationToken = default)
    {
        var query = _db.SavingGoals.AsNoTracking().Where(x => x.UserId == userId);
        if (status is not null)
        {
            query = query.Where(x => x.Status == status);
        }

        return await query
            .OrderBy(x => x.Status)
            .ThenBy(x => x.Deadline)
            .ThenBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, GoalTotals>> GetTotalsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var goalIds = await _db.SavingGoals
            .AsNoTracking()
            .Where(goal => goal.UserId == userId)
            .Select(goal => goal.Id)
            .ToListAsync(cancellationToken);

        if (goalIds.Count == 0)
        {
            return new Dictionary<Guid, GoalTotals>();
        }

        var movements = await _db.GoalMovements
            .AsNoTracking()
            .Where(movement => goalIds.Contains(movement.SavingGoalId))
            .Select(movement => new { movement.SavingGoalId, movement.Type, movement.Amount })
            .ToListAsync(cancellationToken);

        return movements
            .GroupBy(movement => movement.SavingGoalId)
            .ToDictionary(
                g => g.Key,
                g => new GoalTotals(
                    g.Where(x => x.Type == GoalMovementType.Deposit).Sum(x => x.Amount),
                    g.Where(x => x.Type == GoalMovementType.Withdrawal).Sum(x => x.Amount)));
    }

    public async Task<GoalTotals> GetGoalTotalsAsync(Guid goalId, CancellationToken cancellationToken = default)
    {
        var movements = await _db.GoalMovements
            .AsNoTracking()
            .Where(x => x.SavingGoalId == goalId)
            .Select(x => new { x.Type, x.Amount })
            .ToListAsync(cancellationToken);

        if (movements.Count == 0)
        {
            return new GoalTotals(0, 0);
        }

        return new GoalTotals(
            movements.Where(x => x.Type == GoalMovementType.Deposit).Sum(x => x.Amount),
            movements.Where(x => x.Type == GoalMovementType.Withdrawal).Sum(x => x.Amount));
    }

    public async Task<IReadOnlyList<GoalMovement>> ListMovementsAsync(Guid goalId, CancellationToken cancellationToken = default) =>
        await _db.GoalMovements
            .AsNoTracking()
            .Where(x => x.SavingGoalId == goalId)
            .OrderByDescending(x => x.OccurredAt)
            .ThenByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

    public void Add(SavingGoal goal) => _db.SavingGoals.Add(goal);

    public void AddMovement(GoalMovement movement) => _db.GoalMovements.Add(movement);
}

public sealed class NotificationRepository : INotificationRepository
{
    private readonly AppDbContext _db;

    public NotificationRepository(AppDbContext db) => _db = db;

    public Task<Notification?> GetAsync(Guid userId, Guid id, CancellationToken cancellationToken = default) =>
        _db.Notifications.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, cancellationToken);

    public async Task<PagedResult<Notification>> ListAsync(
        Guid userId,
        bool unreadOnly,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Notifications.AsNoTracking().Where(x => x.UserId == userId);
        if (unreadOnly)
        {
            query = query.Where(x => !x.IsRead);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Notification>(items, page, pageSize, total);
    }

    public Task<int> CountUnreadAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _db.Notifications.CountAsync(x => x.UserId == userId && !x.IsRead, cancellationToken);

    public async Task<IReadOnlyList<Notification>> ListUnreadAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await _db.Notifications
            .AsNoTracking()
            .Where(x => x.UserId == userId && !x.IsRead)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

    public Task<bool> ExistsRecentAsync(
        Guid userId,
        NotificationType type,
        Guid resourceId,
        DateTime sinceUtc,
        CancellationToken cancellationToken = default) =>
        _db.Notifications.AnyAsync(
            x => x.UserId == userId
                 && x.Type == type
                 && x.ResourceId == resourceId
                 && x.CreatedAt >= sinceUtc,
            cancellationToken);

    public async Task<int> MarkAllReadAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var notifications = await _db.Notifications
            .Where(x => x.UserId == userId && !x.IsRead)
            .ToListAsync(cancellationToken);

        foreach (var notification in notifications)
        {
            notification.MarkAsRead();
        }

        return notifications.Count;
    }

    public void Add(Notification notification) => _db.Notifications.Add(notification);
}
