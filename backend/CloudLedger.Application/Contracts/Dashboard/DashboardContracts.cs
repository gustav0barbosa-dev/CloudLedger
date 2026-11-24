using CloudLedger.Application.Contracts.Finance;
using CloudLedger.Domain.Goals;
using CloudLedger.Domain.Notifications;
using CloudLedger.Domain.Recurring;

namespace CloudLedger.Application.Contracts.Dashboard;

public sealed record DashboardSummaryDto(
    DateOnly From,
    DateOnly To,
    decimal TotalBalance,
    decimal Income,
    decimal Expense,
    decimal Savings,
    decimal IncomeChangePercent,
    decimal ExpenseChangePercent,
    decimal SavingsChangePercent,
    decimal BalanceChangePercent);

public sealed record SeriesPointDto(DateOnly Date, decimal Income, decimal Expense);

public sealed record CategoryExpenseDto(
    Guid? CategoryId,
    string CategoryName,
    string? Color,
    decimal Total,
    int Count,
    decimal Percentage);

public sealed record UpcomingPaymentDto(
    Guid RecurringPaymentId,
    string Description,
    decimal Amount,
    DateOnly DueDate,
    RecurrenceFrequency Frequency,
    Guid AccountId,
    string AccountName);

public sealed record GoalProgressDto(
    Guid Id,
    string Name,
    decimal TargetAmount,
    decimal Accumulated,
    decimal ProgressPercent,
    DateOnly? Deadline,
    GoalStatus Status);

public sealed record DashboardDto(
    DashboardSummaryDto Summary,
    IReadOnlyList<SeriesPointDto> Series,
    IReadOnlyList<CategoryExpenseDto> ExpensesByCategory,
    IReadOnlyList<TransactionDto> RecentTransactions,
    IReadOnlyList<UpcomingPaymentDto> UpcomingPayments,
    IReadOnlyList<GoalProgressDto> Goals);

public sealed record NotificationDto(
    Guid Id,
    string Title,
    string Message,
    NotificationType Type,
    bool IsRead,
    string? ResourceType,
    Guid? ResourceId,
    DateTime CreatedAt);

public sealed record AuditEventDto(
    Guid Id,
    string Action,
    string ResourceType,
    Guid? ResourceId,
    DateTime OccurredAt,
    IReadOnlyDictionary<string, string?> Metadata);

public sealed record ReportExportDto(string FileName, string Url, DateTime ExpiresAtUtc);
