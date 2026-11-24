using CloudLedger.Domain.Common;

namespace CloudLedger.Domain.Goals;

public sealed class SavingGoal : Entity
{
    private SavingGoal()
    {
    }

    public SavingGoal(Guid userId, string name, decimal targetAmount, DateOnly? deadline, Guid? accountId)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("goal.user_required", "A meta precisa estar vinculada a um usuário.");
        }

        UserId = userId;
        Name = Guard.NotEmpty(name, "nome da meta", 120);
        TargetAmount = Guard.PositiveMoney(targetAmount, "valor-alvo");
        Deadline = deadline;
        AccountId = accountId;
        Status = GoalStatus.Active;
    }

    public Guid UserId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public decimal TargetAmount { get; private set; }

    public DateOnly? Deadline { get; private set; }

    public Guid? AccountId { get; private set; }

    public GoalStatus Status { get; private set; }

    public void Update(string name, decimal targetAmount, DateOnly? deadline, Guid? accountId)
    {
        if (Status == GoalStatus.Cancelled)
        {
            throw new DomainException("goal.cancelled", "Metas canceladas não podem ser editadas.");
        }

        Name = Guard.NotEmpty(name, "nome da meta", 120);
        TargetAmount = Guard.PositiveMoney(targetAmount, "valor-alvo");
        Deadline = deadline;
        AccountId = accountId;
    }

    public void Cancel() => Status = GoalStatus.Cancelled;

    /// <summary>
    /// RN-010: o status é derivado do acumulado real. O percentual visual é limitado
    /// entre 0% e 100%, mas o valor acumulado real é preservado (pode exceder a meta).
    /// </summary>
    public void RecalculateStatus(decimal totalDeposits, decimal totalWithdrawals)
    {
        if (Status == GoalStatus.Cancelled)
        {
            return;
        }

        var accumulated = totalDeposits - totalWithdrawals;
        Status = accumulated >= TargetAmount ? GoalStatus.Achieved : GoalStatus.Active;
    }
}
