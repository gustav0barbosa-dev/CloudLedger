using CloudLedger.Domain.Transactions;
using CloudLedger.Domain.Transfers;

namespace CloudLedger.Application.Abstractions.Persistence;

public sealed record TransactionQuery(
    DateOnly? From,
    DateOnly? To,
    TransactionType? Type,
    Guid? CategoryId,
    Guid? AccountId,
    string? Search,
    TransactionStatus? Status,
    int Page,
    int PageSize,
    string? SortBy,
    string? SortDirection);

public sealed record TransferQuery(
    DateOnly? From,
    DateOnly? To,
    Guid? AccountId,
    int Page,
    int PageSize);

public sealed record CategoryTotal(Guid? CategoryId, string CategoryName, string? Color, decimal Total, int Count);

public sealed record DayTotal(DateOnly Date, decimal Income, decimal Expense);

public sealed record GoalTotals(decimal Deposits, decimal Withdrawals)
{
    public decimal Accumulated => Deposits - Withdrawals;
}
