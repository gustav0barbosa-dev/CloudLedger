namespace CloudLedger.Domain.Recurring;

public enum RecurrenceFrequency
{
    Weekly = 0,
    Monthly = 1,
    Yearly = 2
}

public enum OccurrenceStatus
{
    Pending = 0,
    Paid = 1,
    Skipped = 2
}
