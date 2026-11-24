using System.Globalization;
using System.Text.RegularExpressions;

namespace CloudLedger.Domain.Common;

/// <summary>
/// Validações primitivas reutilizadas pelas entidades. Mantém as invariantes
/// no domínio, independentemente da validação feita na borda HTTP.
/// </summary>
public static partial class Guard
{
    public const decimal MaxMoney = 999_999_999_999.99m;

    public static string NotEmpty(string? value, string field, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException($"{field}.required", $"O campo {field} é obrigatório.");
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new DomainException($"{field}.too_long", $"O campo {field} deve ter no máximo {maxLength} caracteres.");
        }

        return trimmed;
    }

    public static decimal PositiveMoney(decimal value, string field)
    {
        if (value <= 0)
        {
            throw new DomainException($"{field}.not_positive", "O valor deve ser maior que zero.");
        }

        if (value > MaxMoney)
        {
            throw new DomainException($"{field}.too_large", "O valor informado excede o limite permitido.");
        }

        if (decimal.Round(value, 2) != value)
        {
            throw new DomainException($"{field}.scale", "O valor deve ter no máximo duas casas decimais.");
        }

        return value;
    }

    public static decimal Money(decimal value, string field)
    {
        if (Math.Abs(value) > MaxMoney)
        {
            throw new DomainException($"{field}.too_large", "O valor informado excede o limite permitido.");
        }

        if (decimal.Round(value, 2) != value)
        {
            throw new DomainException($"{field}.scale", "O valor deve ter no máximo duas casas decimais.");
        }

        return value;
    }

    public static string Email(string? value)
    {
        var email = NotEmpty(value, "e-mail", 256).ToLowerInvariant();

        if (!EmailPattern().IsMatch(email))
        {
            throw new DomainException("email.invalid", "Informe um e-mail válido.");
        }

        return email;
    }

    public static DateOnly NotFuture(DateOnly value, DateOnly today, string field)
    {
        if (value > today)
        {
            throw new DomainException($"{field}.future", "A data não pode estar no futuro.");
        }

        return value;
    }

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]{2,}$", RegexOptions.CultureInvariant)]
    private static partial Regex EmailPattern();

    public static string Money2(decimal value) =>
        value.ToString("0.00", CultureInfo.InvariantCulture);
}
