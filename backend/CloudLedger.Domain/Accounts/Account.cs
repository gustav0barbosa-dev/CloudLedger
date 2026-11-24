using CloudLedger.Domain.Common;

namespace CloudLedger.Domain.Accounts;

public enum AccountType
{
    Checking = 0,
    Savings = 1,
    Cash = 2,
    Investment = 3,
    Credit = 4
}

public sealed class Account : Entity
{
    public const string DefaultCurrency = "BRL";

    private Account()
    {
    }

    public Account(Guid userId, string name, AccountType type, decimal openingBalance, string currency = DefaultCurrency)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("account.user_required", "A conta precisa estar vinculada a um usuário.");
        }

        UserId = userId;
        Name = Guard.NotEmpty(name, "nome da conta", 120);
        Type = type;
        OpeningBalance = Guard.Money(openingBalance, "saldo inicial");
        Currency = NormalizeCurrency(currency);
        IsArchived = false;
    }

    public Guid UserId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public AccountType Type { get; private set; }

    public string Currency { get; private set; } = DefaultCurrency;

    public decimal OpeningBalance { get; private set; }

    public bool IsArchived { get; private set; }

    public void UpdateDetails(string name, AccountType type, decimal openingBalance)
    {
        if (IsArchived)
        {
            throw new DomainException("account.archived", "Não é possível editar uma conta arquivada.");
        }

        Name = Guard.NotEmpty(name, "nome da conta", 120);
        Type = type;
        OpeningBalance = Guard.Money(openingBalance, "saldo inicial");
    }

    public void Archive()
    {
        if (IsArchived)
        {
            return;
        }

        IsArchived = true;
    }

    public void Unarchive() => IsArchived = false;

    public void EnsureCanReceiveMovements()
    {
        if (IsArchived)
        {
            throw new DomainException("account.archived", "A conta está arquivada e não aceita movimentações.");
        }
    }

    private static string NormalizeCurrency(string currency)
    {
        var value = Guard.NotEmpty(currency, "moeda", 3).ToUpperInvariant();
        if (value.Length != 3)
        {
            throw new DomainException("account.currency", "A moeda deve ter três letras (ISO 4217).");
        }

        return value;
    }
}
