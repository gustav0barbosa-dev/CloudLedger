using CloudLedger.Domain.Common;

namespace CloudLedger.UnitTests.Domain;

public class GuardTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void PositiveMoney_NonPositive_Throws(decimal value)
    {
        Assert.Throws<DomainException>(() => Guard.PositiveMoney(value, "valor"));
    }

    [Fact]
    public void PositiveMoney_TooManyDecimals_Throws()
    {
        Assert.Throws<DomainException>(() => Guard.PositiveMoney(1.234m, "valor"));
    }

    [Fact]
    public void PositiveMoney_ExceedsMax_Throws()
    {
        Assert.Throws<DomainException>(() => Guard.PositiveMoney(Guard.MaxMoney + 1m, "valor"));
    }

    [Fact]
    public void PositiveMoney_Valid_ReturnsValue()
    {
        Assert.Equal(12.34m, Guard.PositiveMoney(12.34m, "valor"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NotEmpty_Blank_Throws(string? value)
    {
        Assert.Throws<DomainException>(() => Guard.NotEmpty(value, "nome", 10));
    }

    [Fact]
    public void NotEmpty_TooLong_Throws()
    {
        Assert.Throws<DomainException>(() => Guard.NotEmpty("12345678901", "nome", 10));
    }

    [Fact]
    public void NotEmpty_Valid_Trims()
    {
        Assert.Equal("abc", Guard.NotEmpty("  abc  ", "nome", 10));
    }

    [Theory]
    [InlineData("invalido")]
    [InlineData("@semlocal.com")]
    [InlineData("sem@dominio")]
    public void Email_Invalid_Throws(string value)
    {
        Assert.Throws<DomainException>(() => Guard.Email(value));
    }

    [Fact]
    public void Email_Valid_NormalizesToLower()
    {
        Assert.Equal("user@example.com", Guard.Email("User@Example.com"));
    }

    [Fact]
    public void NotFuture_FutureDate_Throws()
    {
        var today = new DateOnly(2026, 1, 1);
        Assert.Throws<DomainException>(() => Guard.NotFuture(new DateOnly(2026, 1, 2), today, "data"));
    }

    [Fact]
    public void NotFuture_PastOrToday_ReturnsValue()
    {
        var today = new DateOnly(2026, 1, 1);
        Assert.Equal(today, Guard.NotFuture(today, today, "data"));
    }
}