using CloudLedger.Domain.Common;

namespace CloudLedger.Domain.Goals;

public sealed class GoalMovement : Entity
{
    private GoalMovement()
    {
    }

    public GoalMovement(Guid savingGoalId, decimal amount, GoalMovementType type, DateOnly occurredAt)
    {
        if (savingGoalId == Guid.Empty)
        {
            throw new DomainException("goal_movement.goal_required", "O movimento precisa estar vinculado a uma meta.");
        }

        SavingGoalId = savingGoalId;
        Amount = Guard.PositiveMoney(amount, "valor");
        Type = type;
        OccurredAt = occurredAt;
    }

    public Guid SavingGoalId { get; private set; }

    public decimal Amount { get; private set; }

    public GoalMovementType Type { get; private set; }

    public DateOnly OccurredAt { get; private set; }

    public decimal SignedAmount => Type == GoalMovementType.Deposit ? Amount : -Amount;
}
