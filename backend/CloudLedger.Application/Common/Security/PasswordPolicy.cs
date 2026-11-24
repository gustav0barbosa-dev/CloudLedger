namespace CloudLedger.Application.Common.Security;

public static class PasswordPolicy
{
    public const int MinLength = 8;
    public const int MaxLength = 128;

    public static void EnsureValid(string? password)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(password) || password.Length < MinLength)
        {
            errors["password"] = [$"A senha deve ter no mínimo {MinLength} caracteres."];
        }
        else if (password.Length > MaxLength)
        {
            errors["password"] = [$"A senha deve ter no máximo {MaxLength} caracteres."];
        }
        else if (!password.Any(char.IsLetter) || !password.Any(char.IsDigit))
        {
            errors["password"] = ["A senha deve conter ao menos uma letra e um número."];
        }

        if (errors.Count > 0)
        {
            throw new Exceptions.ValidationAppException(errors);
        }
    }
}
