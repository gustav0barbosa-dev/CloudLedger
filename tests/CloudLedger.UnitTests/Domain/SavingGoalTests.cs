using CloudLedger.Domain.Common;
using CloudLedger.Domain.Goals;

namespace CloudLedger.UnitTests.Domain;

public class SavingGoalTests
{
    [Fact]
    public void Constructor_InvalidTarget_Throws()
    {
        Assert.Throws<DomainException>(() => new SavingGoal(Guid.NewGuid(), "Meta", 0m, null, null));
    }

    [Fact]
    public void RecalculateStatus_Achieved_WhenAccumulatedReachesTarget()
    {
        var goal = new SavingGoal(Guid.NewGuid(), "Meta", 100m, null, null);

        goal.RecalculateStatus(100m, 0m);

        Assert.Equal(GoalStatus.Achieved, goal.Status);
    }

    [Fact]
    public void RecalculateStatus_Active_WhenBelowTarget()
    {
        var goal = new SavingGoal(Guid.NewGuid(), "Meta", 100m, null, null);

        goal.RecalculateStatus(50m, 0m);

        Assert.Equal(GoalStatus.Active, goal.Status);
    }

    [Fact]
    public void RecalculateStatus_Cancelled_IsIgnored()
    {
        var goal = new SavingGoal(Guid.NewGuid(), "Meta", 100m, null, null);
        goal.Cancel();

        goal.RecalculateStatus(200m, 0m);

        Assert.Equal(GoalStatus.Cancelled, goal.Status);
    }

    [Fact]
    public void CancelledGoal_CannotBeEdited()
    {
        var goal = new SavingGoal(Guid.NewGuid(), "Meta", 100m, null, null);
        goal.Cancel();

        Assert.Throws<DomainException>(() => goal.Update("Outro", 200m, null, null));
    }

    [Fact]
    public void Update_Valid_ChangesFields()
    {
        var goal = new SavingGoal(Guid.NewGuid(), "Meta", 100m, null, null);

        goal.Update("Nova", 200m, new DateOnly(2027, 1, 1), null);

        Assert.Equal("Nova", goal.Name);
        Assert.Equal(200m, goal.TargetAmount);
        Assert.Equal(new DateOnly(2027, 1, 1), goal.Deadline);
    }
}