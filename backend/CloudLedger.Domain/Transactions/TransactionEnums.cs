namespace CloudLedger.Domain.Transactions;

public enum TransactionType
{
    Income = 0,
    Expense = 1
}

public enum TransactionStatus
{
    Confirmed = 0,
    Voided = 1
}
