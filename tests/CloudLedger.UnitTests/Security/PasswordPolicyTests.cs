using CloudLedger.Application.Common.Security;

namespace CloudLedger.UnitTests.Security;

public class PasswordPolicyTests
{
    [Theory]
    [InlineData("curta1")]
    [InlineData("semnumero")]
    [InlineData("12345678")]
    [InlineData("")]
    public void EnsureValid_Invalid_Throws(string password)
    {
        Assert.Throws<CloudLedger.Application.Common.Exceptions.ValidationAppException>(() => PasswordPolicy.EnsureValid(password));
    }

    [Fact]
    public void EnsureValid_TooLong_Throws()
    {
        var longPwd = new string('a', 129) + "1";
        Assert.Throws<CloudLedger.Application.Common.Exceptions.ValidationAppException>(() => PasswordPolicy.EnsureValid(longPwd));
    }

    [Fact]
    public void EnsureValid_Valid_DoesNotThrow()
    {
        PasswordPolicy.EnsureValid("Senha123");
    }
}