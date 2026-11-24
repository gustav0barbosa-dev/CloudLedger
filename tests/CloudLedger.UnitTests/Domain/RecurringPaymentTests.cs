using CloudLedger.Domain.Recurring;

namespace CloudLedger.UnitTests.Domain;

public class RecurringPaymentTests
{
    private static RecurringPayment CreateMonthly(DateOnly start) =>
        new(Guid.NewGuid(), Guid.NewGuid(), null, "Assinatura", 29.90m, RecurrenceFrequency.Monthly, start);

    [Fact]
    public void ProjectOccurrences_Monthly_ReturnsExpectedDates()
    {
        var payment = CreateMonthly(new DateOnly(2026, 1, 1));

        var occurrences = payment.ProjectOccurrences(new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 31));

        Assert.Equal(3, occurrences.Count);
        Assert.Equal(new DateOnly(2026, 1, 1), occurrences[0]);
        Assert.Equal(new DateOnly(2026, 2, 1), occurrences[1]);
        Assert.Equal(new DateOnly(2026, 3, 1), occurrences[2]);
    }

    [Fact]
    public void ProjectOccurrences_Inactive_ReturnsEmpty()
    {
        var payment = CreateMonthly(new DateOnly(2026, 1, 1));
        payment.Deactivate();

        var occurrences = payment.ProjectOccurrences(new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 31));

        Assert.Empty(occurrences);
    }

    [Fact]
    public void IsDueDate_ValidOccurrence_ReturnsTrue()
    {
        var payment = CreateMonthly(new DateOnly(2026, 1, 1));

        Assert.True(payment.IsDueDate(new DateOnly(2026, 2, 1)));
    }

    [Fact]
    public void IsDueDate_InvalidDate_ReturnsFalse()
    {
        var payment = CreateMonthly(new DateOnly(2026, 1, 1));

        Assert.False(payment.IsDueDate(new DateOnly(2026, 1, 15)));
    }

    [Fact]
    public void AdvanceAfter_MovesNextDueDate()
    {
        var payment = CreateMonthly(new DateOnly(2026, 1, 1));

        payment.AdvanceAfter(new DateOnly(2026, 1, 1));

        Assert.Equal(new DateOnly(2026, 2, 1), payment.NextDueDate);
    }

    [Fact]
    public void NextDateAfter_Weekly_AddsSevenDays()
    {
        var payment = new RecurringPayment(Guid.NewGuid(), Guid.NewGuid(), null, "X", 10m, RecurrenceFrequency.Weekly, new DateOnly(2026, 1, 1));

        Assert.Equal(new DateOnly(2026, 1, 8), payment.NextDateAfter(new DateOnly(2026, 1, 1)));
    }

    [Fact]
    public void ProjectOccurrences_MaxOccurrences_Respected()
    {
        var payment = CreateMonthly(new DateOnly(2026, 1, 1));

        var occurrences = payment.ProjectOccurrences(new DateOnly(2026, 1, 1), new DateOnly(2030, 12, 31), maxOccurrences: 3);

        Assert.Equal(3, occurrences.Count);
    }
}