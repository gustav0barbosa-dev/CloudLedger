using CloudLedger.Domain.Common;

namespace CloudLedger.Domain.Transfers;

public enum TransferStatus
{
    Confirmed = 0,
    Voided = 1
}

/// <summary>
/// Transferência simulada entre duas contas do mesmo usuário (RN-005).
/// Não é receita nem despesa no consolidado (RN-006).
/// </summary>
public sealed class Transfer : Entity
{
    private Transfer()
    {
    }

    public Transfer(
        Guid userId,
        Guid sourceAccountId,
        Guid destinationAccountId,
        decimal amount,
        DateOnly occurredAt,
        string? description)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("transfer.user_required", "A transferência precisa estar vinculada a um usuário.");
        }

        if (sourceAccountId == Guid.Empty || destinationAccountId == Guid.Empty)
        {
            throw new DomainException("transfer.account_required", "Informe as contas de origem e destino.");
        }

        if (sourceAccountId == destinationAccountId)
        {
            throw new DomainException("transfer.same_account", "As contas de origem e destino devem ser diferentes.");
        }

        UserId = userId;
        SourceAccountId = sourceAccountId;
        DestinationAccountId = destinationAccountId;
        Amount = Guard.PositiveMoney(amount, "valor");
        OccurredAt = occurredAt;
        Description = string.IsNullOrWhiteSpace(description) ? null : Guard.NotEmpty(description, "descrição", 200);
        Status = TransferStatus.Confirmed;
    }

    public Guid UserId { get; private set; }

    public Guid SourceAccountId { get; private set; }

    public Guid DestinationAccountId { get; private set; }

    public decimal Amount { get; private set; }

    public DateOnly OccurredAt { get; private set; }

    public string? Description { get; private set; }

    public TransferStatus Status { get; private set; }

    public DateTime? VoidedAt { get; private set; }

    public bool IsConfirmed => Status == TransferStatus.Confirmed;

    public void Void(DateTime utcNow)
    {
        if (!IsConfirmed)
        {
            throw new DomainException("transfer.voided", "A transferência já foi estornada.");
        }

        Status = TransferStatus.Voided;
        VoidedAt = utcNow;
    }
}
