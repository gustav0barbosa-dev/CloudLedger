using CloudLedger.Domain.Common;

namespace CloudLedger.Domain.Recurring;

/// <summary>
/// Estado persistido de uma ocorrência. A combinação (RecurringPaymentId, DueDate)
/// é única, garantindo RN-008 (um lançamento por ocorrência).
/// </summary>
public sealed class RecurringOccurrence : Entity
{
    private RecurringOccurrence()
    {
    }

    public RecurringOccurrence(Guid recurringPaymentId, DateOnly dueDate)
    {
        if (recurringPaymentId == Guid.Empty)
        {
            throw new DomainException("occurrence.recurring_required", "A ocorrência precisa estar vinculada a um pagamento recorrente.");
        }

        RecurringPaymentId = recurringPaymentId;
        DueDate = dueDate;
        Status = OccurrenceStatus.Pending;
    }

    public Guid RecurringPaymentId { get; private set; }

    public DateOnly DueDate { get; private set; }

    public OccurrenceStatus Status { get; private set; }

    public Guid? TransactionId { get; private set; }

    public DateTime? PaidAt { get; private set; }

    public void MarkAsPaid(Guid transactionId, DateTime utcNow)
    {
        if (Status == OccurrenceStatus.Paid)
        {
            throw new DomainException("occurrence.already_paid", "Esta ocorrência já foi paga.");
        }

        if (transactionId == Guid.Empty)
        {
            throw new DomainException("occurrence.transaction_required", "A ocorrência paga deve referenciar um lançamento.");
        }

        Status = OccurrenceStatus.Paid;
        TransactionId = transactionId;
        PaidAt = utcNow;
    }

    public void Skip()
    {
        if (Status == OccurrenceStatus.Paid)
        {
            throw new DomainException("occurrence.already_paid", "Ocorrências pagas não podem ser ignoradas.");
        }

        Status = OccurrenceStatus.Skipped;
    }
}
