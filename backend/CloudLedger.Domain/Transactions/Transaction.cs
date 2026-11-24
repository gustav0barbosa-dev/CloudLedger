using CloudLedger.Domain.Common;

namespace CloudLedger.Domain.Transactions;

public sealed class Transaction : Entity
{
    private Transaction()
    {
    }

    public Transaction(
        Guid userId,
        Guid accountId,
        Guid? categoryId,
        TransactionType type,
        decimal amount,
        string description,
        DateOnly occurredAt)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("transaction.user_required", "O lançamento precisa estar vinculado a um usuário.");
        }

        if (accountId == Guid.Empty)
        {
            throw new DomainException("transaction.account_required", "O lançamento precisa estar vinculado a uma conta.");
        }

        UserId = userId;
        AccountId = accountId;
        CategoryId = categoryId;
        Type = type;
        Amount = Guard.PositiveMoney(amount, "valor");
        Description = Guard.NotEmpty(description, "descrição", 200);
        OccurredAt = occurredAt;
        Status = TransactionStatus.Confirmed;
    }

    public Guid UserId { get; private set; }

    public Guid AccountId { get; private set; }

    public Guid? CategoryId { get; private set; }

    public TransactionType Type { get; private set; }

    public decimal Amount { get; private set; }

    public string Description { get; private set; } = string.Empty;

    public DateOnly OccurredAt { get; private set; }

    public TransactionStatus Status { get; private set; }

    public DateTime? VoidedAt { get; private set; }

    public Guid? ReversalOfId { get; private set; }

    public bool IsConfirmed => Status == TransactionStatus.Confirmed;

    /// <summary>Efeito do lançamento no saldo da conta: receita soma, despesa subtrai.</summary>
    public decimal SignedAmount => Type == TransactionType.Income ? Amount : -Amount;

    public void Update(Guid accountId, Guid? categoryId, TransactionType type, decimal amount, string description, DateOnly occurredAt)
    {
        EnsureConfirmed();

        if (accountId == Guid.Empty)
        {
            throw new DomainException("transaction.account_required", "O lançamento precisa estar vinculado a uma conta.");
        }

        AccountId = accountId;
        CategoryId = categoryId;
        Type = type;
        Amount = Guard.PositiveMoney(amount, "valor");
        Description = Guard.NotEmpty(description, "descrição", 200);
        OccurredAt = occurredAt;
    }

    /// <summary>Estorno lógico (RN-007): preserva o registro e o marca como cancelado.</summary>
    public void Void(DateTime utcNow)
    {
        EnsureConfirmed();
        Status = TransactionStatus.Voided;
        VoidedAt = utcNow;
    }

    public void MarkAsReversalOf(Guid originalTransactionId)
    {
        if (originalTransactionId == Guid.Empty || originalTransactionId == Id)
        {
            throw new DomainException("transaction.reversal_invalid", "Referência de estorno inválida.");
        }

        ReversalOfId = originalTransactionId;
    }

    private void EnsureConfirmed()
    {
        if (!IsConfirmed)
        {
            throw new DomainException("transaction.voided", "Lançamentos estornados não podem ser alterados.");
        }
    }
}
