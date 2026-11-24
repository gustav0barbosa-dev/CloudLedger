using CloudLedger.Application.Abstractions;
using CloudLedger.Application.Abstractions.Persistence;
using CloudLedger.Application.Common;
using CloudLedger.Application.Common.Exceptions;
using CloudLedger.Application.Contracts.Planning;
using CloudLedger.Domain.Audit;
using CloudLedger.Domain.Events;
using CloudLedger.Domain.Goals;
using CloudLedger.Domain.Notifications;

namespace CloudLedger.Application.Services;

public sealed class GoalService
{
    private readonly ISavingGoalRepository _goals;
    private readonly IAccountRepository _accounts;
    private readonly INotificationRepository _notifications;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditWriter _audit;
    private readonly IDomainEventPublisher _events;
    private readonly ICorrelationContext _correlation;
    private readonly IClock _clock;

    public GoalService(
        ISavingGoalRepository goals,
        IAccountRepository accounts,
        INotificationRepository notifications,
        IUnitOfWork unitOfWork,
        IAuditWriter audit,
        IDomainEventPublisher events,
        ICorrelationContext correlation,
        IClock clock)
    {
        _goals = goals;
        _accounts = accounts;
        _notifications = notifications;
        _unitOfWork = unitOfWork;
        _audit = audit;
        _events = events;
        _correlation = correlation;
        _clock = clock;
    }

    public async Task<IReadOnlyList<GoalDto>> ListAsync(Guid userId, GoalStatus? status, CancellationToken cancellationToken = default)
    {
        var goals = await _goals.ListAsync(userId, status, cancellationToken);
        var totals = await _goals.GetTotalsAsync(userId, cancellationToken);

        return goals
            .Select(goal => ToDto(goal, totals.GetValueOrDefault(goal.Id) ?? new GoalTotals(0, 0)))
            .ToList();
    }

    public async Task<GoalDto> GetAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        var goal = await _goals.GetAsync(userId, id, cancellationToken)
            ?? throw new NotFoundException("Meta");

        var totals = await _goals.GetGoalTotalsAsync(goal.Id, cancellationToken);
        return ToDto(goal, totals);
    }

    public async Task<GoalDto> CreateAsync(Guid userId, CreateGoalRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureAccountIsUsableAsync(userId, request.AccountId, cancellationToken);

        var goal = new SavingGoal(userId, request.Name, request.TargetAmount, request.Deadline, request.AccountId);
        _goals.Add(goal);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _audit.WriteAsync(
            AuditEventFactory.Create(userId, AuditActions.GoalCreated, nameof(SavingGoal), goal.Id, _correlation.CorrelationId, _clock.UtcNow,
                ("name", goal.Name), ("targetAmount", goal.TargetAmount.ToString("0.00"))),
            cancellationToken);

        return ToDto(goal, new GoalTotals(0, 0));
    }

    public async Task<GoalDto> UpdateAsync(Guid userId, Guid id, UpdateGoalRequest request, CancellationToken cancellationToken = default)
    {
        var goal = await _goals.GetAsync(userId, id, cancellationToken)
            ?? throw new NotFoundException("Meta");

        await EnsureAccountIsUsableAsync(userId, request.AccountId, cancellationToken);

        goal.Update(request.Name, request.TargetAmount, request.Deadline, request.AccountId);
        var totals = await _goals.GetGoalTotalsAsync(goal.Id, cancellationToken);
        goal.RecalculateStatus(totals.Deposits, totals.Withdrawals);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _audit.WriteAsync(
            AuditEventFactory.Create(userId, AuditActions.GoalUpdated, nameof(SavingGoal), goal.Id, _correlation.CorrelationId, _clock.UtcNow,
                ("name", goal.Name), ("targetAmount", goal.TargetAmount.ToString("0.00"))),
            cancellationToken);

        return ToDto(goal, totals);
    }

    public async Task<GoalDto> CancelAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        var goal = await _goals.GetAsync(userId, id, cancellationToken)
            ?? throw new NotFoundException("Meta");

        goal.Cancel();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _audit.WriteAsync(
            AuditEventFactory.Create(userId, AuditActions.GoalCancelled, nameof(SavingGoal), goal.Id, _correlation.CorrelationId, _clock.UtcNow),
            cancellationToken);

        var totals = await _goals.GetGoalTotalsAsync(goal.Id, cancellationToken);
        return ToDto(goal, totals);
    }

    public async Task<IReadOnlyList<GoalMovementDto>> ListMovementsAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        var goal = await _goals.GetAsync(userId, id, cancellationToken)
            ?? throw new NotFoundException("Meta");

        var movements = await _goals.ListMovementsAsync(goal.Id, cancellationToken);
        return movements.Select(ToDto).ToList();
    }

    public async Task<GoalMovementDto> AddMovementAsync(
        Guid userId,
        Guid id,
        CreateGoalMovementRequest request,
        CancellationToken cancellationToken = default)
    {
        var goal = await _goals.GetAsync(userId, id, cancellationToken)
            ?? throw new NotFoundException("Meta");

        if (goal.Status == GoalStatus.Cancelled)
        {
            throw new ValidationAppException("goal", "Metas canceladas não aceitam movimentos.");
        }

        var movement = new GoalMovement(goal.Id, request.Amount, request.Type, request.OccurredAt);
        var current = await _goals.GetGoalTotalsAsync(goal.Id, cancellationToken);

        var newDeposits = current.Deposits + (request.Type == GoalMovementType.Deposit ? movement.Amount : 0);
        var newWithdrawals = current.Withdrawals + (request.Type == GoalMovementType.Withdrawal ? movement.Amount : 0);

        if (newDeposits - newWithdrawals < 0)
        {
            throw new ValidationAppException("amount", "A retirada não pode ser maior que o valor acumulado na meta.");
        }

        var previousStatus = goal.Status;
        _goals.AddMovement(movement);
        goal.RecalculateStatus(newDeposits, newWithdrawals);

        if (previousStatus != GoalStatus.Achieved && goal.Status == GoalStatus.Achieved)
        {
            _notifications.Add(new Notification(
                userId,
                "Meta atingida",
                $"Parabéns! Você atingiu a meta \"{goal.Name}\".",
                NotificationType.GoalAchieved,
                nameof(SavingGoal),
                goal.Id));
        }

        await _events.PublishAsync(
            new DomainEvent(Guid.NewGuid(), AuditActions.GoalMovementCreated, userId, goal.Id, _clock.UtcNow,
                new Dictionary<string, string?>
                {
                    ["type"] = movement.Type.ToString(),
                    ["amount"] = movement.Amount.ToString("0.00"),
                    ["status"] = goal.Status.ToString()
                }),
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _audit.WriteAsync(
            AuditEventFactory.Create(userId, AuditActions.GoalMovementCreated, nameof(SavingGoal), goal.Id, _correlation.CorrelationId, _clock.UtcNow,
                ("type", movement.Type.ToString()), ("amount", movement.Amount.ToString("0.00"))),
            cancellationToken);

        return ToDto(movement);
    }

    private async Task EnsureAccountIsUsableAsync(Guid userId, Guid? accountId, CancellationToken cancellationToken)
    {
        if (accountId is null)
        {
            return;
        }

        var account = await _accounts.GetAsync(userId, accountId.Value, includeArchived: false, cancellationToken)
            ?? throw new NotFoundException("Conta");
    }

    private static GoalDto ToDto(SavingGoal goal, GoalTotals totals)
    {
        var percent = goal.TargetAmount <= 0
            ? 0
            : Math.Round(Math.Clamp(totals.Accumulated / goal.TargetAmount * 100m, 0m, 100m), 1);

        return new GoalDto(
            goal.Id,
            goal.Name,
            goal.TargetAmount,
            totals.Accumulated,
            percent,
            goal.Deadline,
            goal.AccountId,
            goal.Status,
            goal.CreatedAt);
    }

    private static GoalMovementDto ToDto(GoalMovement movement) => new(
        movement.Id,
        movement.SavingGoalId,
        movement.Amount,
        movement.Type,
        movement.OccurredAt,
        movement.CreatedAt);
}
