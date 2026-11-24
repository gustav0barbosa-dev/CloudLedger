namespace CloudLedger.Domain.Goals;

public enum GoalStatus
{
    Active = 0,
    Achieved = 1,
    Cancelled = 2
}

public enum GoalMovementType
{
    Deposit = 0,
    Withdrawal = 1
}
