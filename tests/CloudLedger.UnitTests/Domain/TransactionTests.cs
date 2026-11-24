using CloudLedger.Domain.Common;
using CloudLedger.Domain.Transactions;

namespace CloudLedger.UnitTests.Domain;

public class TransactionTests
{
    private static Transaction Create() =>
        new(Guid.NewGuid(), Guid.NewGuid(), null, TransactionType.Expense, 100m, "Despesa", new DateOnly(2026, 1, 1));

    [Fact]
    public void SignedAmount_Expense_IsNegative()
    {
        var t = Create();
        Assert.Equal(-100m, t.SignedAmount);
    }

    [Fact]
    public void SignedAmount_Income_IsPositive()
    {
        var t = new Transaction(Guid.NewGuid(), Guid.NewGuid(), null, TransactionType.Income, 50m, "Receita", new DateOnly(2026, 1, 1));
        Assert.Equal(50m, t.SignedAmount);
    }

    [Fact]
    public void Void_MarksVoided()
    {
        var t = Create();
        t.Void(new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc));
        Assert.Equal(TransactionStatus.Voided, t.Status);
        Assert.NotNull(t.VoidedAt);
    }

    [Fact]
    public void Void_Twice_Throws()
    {
        var t = Create();
        t.Void(DateTime.UtcNow);
        Assert.Throws<DomainException>(() => t.Void(DateTime.UtcNow));
    }

    [Fact]
    public void Update_WhenVoided_Throws()
    {
        var t = Create();
        t.Void(DateTime.UtcNow);
        Assert.Throws<DomainException>(() => t.Update(t.AccountId, null, TransactionType.Expense, 50m, "x", new DateOnly(2026, 1, 2)));
    }

    [Fact]
    public void Constructor_InvalidAmount_Throws()
    {
        Assert.Throws<DomainException>(() => new Transaction(Guid.NewGuid(), Guid.NewGuid(), null, TransactionType.Expense, 0m, "x", new DateOnly(2026, 1, 1)));
    }
}