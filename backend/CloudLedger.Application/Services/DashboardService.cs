using CloudLedger.Application.Abstractions;
using CloudLedger.Application.Abstractions.Persistence;
using CloudLedger.Application.Common.Exceptions;
using CloudLedger.Application.Contracts.Dashboard;
using CloudLedger.Domain.Recurring;
using CloudLedger.Domain.Transactions;

namespace CloudLedger.Application.Services;

public enum SeriesGranularity
{
    Day = 0,
    Month = 1
}

public sealed class DashboardService
{
    private const int MaxPeriodDays = 366;

    private readonly ITransactionRepository _transactions;
    private readonly IAccountRepository _accounts;
    private readonly IAccountBalanceReader _balances;
    private readonly IRecurringPaymentRepository _recurring;
    private readonly IRecurringOccurrenceRepository _occurrences;
    private readonly GoalService _goals;
    private readonly IClock _clock;

    public DashboardService(
        ITransactionRepository transactions,
        IAccountRepository accounts,
        IAccountBalanceReader balances,
        IRecurringPaymentRepository recurring,
        IRecurringOccurrenceRepository occurrences,
        GoalService goals,
        IClock clock)
    {
        _transactions = transactions;
        _accounts = accounts;
        _balances = balances;
        _recurring = recurring;
        _occurrences = occurrences;
        _goals = goals;
        _clock = clock;
    }

    public async Task<DashboardDto> GetAsync(Guid userId, DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default)
    {
        var (start, end) = ResolvePeriod(from, to);

        var summary = await GetSummaryAsync(userId, start, end, cancellationToken);
        var series = await GetSeriesAsync(userId, start, end, SeriesGranularity.Day, cancellationToken);
        var categories = await GetExpensesByCategoryAsync(userId, start, end, cancellationToken);
        var recent = await _transactions.ListRecentAsync(userId, 5, cancellationToken);
        var upcoming = await GetUpcomingPaymentsAsync(userId, 30, cancellationToken);
        var goals = (await _goals.ListAsync(userId, null, cancellationToken))
            .Where(x => x.Status != Domain.Goals.GoalStatus.Cancelled)
            .Take(4)
            .Select(x => new GoalProgressDto(x.Id, x.Name, x.TargetAmount, x.Accumulated, x.ProgressPercent, x.Deadline, x.Status))
            .ToList();

        return new DashboardDto(summary, series, categories, recent, upcoming, goals);
    }

    public async Task<DashboardSummaryDto> GetSummaryAsync(Guid userId, DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default)
    {
        var (start, end) = ResolvePeriod(from, to);
        var length = end.DayNumber - start.DayNumber + 1;

        var previousEnd = start.AddDays(-1);
        var previousStart = previousEnd.AddDays(-(length - 1));

        var income = await _transactions.SumAsync(userId, TransactionType.Income, start, end, cancellationToken);
        var expense = await _transactions.SumAsync(userId, TransactionType.Expense, start, end, cancellationToken);
        var previousIncome = await _transactions.SumAsync(userId, TransactionType.Income, previousStart, previousEnd, cancellationToken);
        var previousExpense = await _transactions.SumAsync(userId, TransactionType.Expense, previousStart, previousEnd, cancellationToken);

        var balance = await _balances.GetTotalBalanceAsync(userId, end, cancellationToken);
        var previousBalance = await _balances.GetTotalBalanceAsync(userId, previousEnd, cancellationToken);

        return new DashboardSummaryDto(
            start,
            end,
            balance,
            income,
            expense,
            income - expense,
            PercentChange(income, previousIncome),
            PercentChange(expense, previousExpense),
            PercentChange(income - expense, previousIncome - previousExpense),
            PercentChange(balance, previousBalance));
    }

    public async Task<IReadOnlyList<SeriesPointDto>> GetSeriesAsync(
        Guid userId,
        DateOnly? from,
        DateOnly? to,
        SeriesGranularity granularity = SeriesGranularity.Day,
        CancellationToken cancellationToken = default)
    {
        var (start, end) = ResolvePeriod(from, to);
        var totals = await _transactions.SumByDayAsync(userId, start, end, cancellationToken);
        var byDate = totals.ToDictionary(x => x.Date);

        if (granularity == SeriesGranularity.Month)
        {
            var points = new List<SeriesPointDto>();
            var cursor = new DateOnly(start.Year, start.Month, 1);
            while (cursor <= end)
            {
                var monthStart = cursor;
                var monthEnd = cursor.AddMonths(1).AddDays(-1);
                var monthTotals = totals.Where(x => x.Date >= monthStart && x.Date <= monthEnd).ToList();
                points.Add(new SeriesPointDto(
                    monthStart,
                    monthTotals.Sum(x => x.Income),
                    monthTotals.Sum(x => x.Expense)));
                cursor = cursor.AddMonths(1);
            }

            return points;
        }

        var days = new List<SeriesPointDto>(end.DayNumber - start.DayNumber + 1);
        for (var date = start; date <= end; date = date.AddDays(1))
        {
            var total = byDate.GetValueOrDefault(date);
            days.Add(new SeriesPointDto(date, total?.Income ?? 0, total?.Expense ?? 0));
        }

        return days;
    }

    public async Task<IReadOnlyList<CategoryExpenseDto>> GetExpensesByCategoryAsync(
        Guid userId,
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken = default)
    {
        var (start, end) = ResolvePeriod(from, to);
        var totals = await _transactions.SumByCategoryAsync(userId, start, end, cancellationToken);
        var totalExpense = totals.Sum(x => x.Total);

        return totals
            .OrderByDescending(x => x.Total)
            .Select(item => new CategoryExpenseDto(
                item.CategoryId,
                item.CategoryName,
                item.Color,
                item.Total,
                item.Count,
                totalExpense == 0 ? 0 : Math.Round(item.Total / totalExpense * 100, 1)))
            .ToList();
    }

    public async Task<IReadOnlyList<Contracts.Finance.TransactionDto>> GetRecentTransactionsAsync(
        Guid userId,
        int take,
        CancellationToken cancellationToken = default)
    {
        var normalized = Math.Clamp(take, 1, 50);
        return await _transactions.ListRecentAsync(userId, normalized, cancellationToken);
    }

    public async Task<IReadOnlyList<UpcomingPaymentDto>> GetUpcomingPaymentsAsync(
        Guid userId,
        int days,
        CancellationToken cancellationToken = default)
    {        var horizon = Math.Clamp(days, 1, 365);
        var start = _clock.Today;
        var end = start.AddDays(horizon);

        var payments = await _recurring.ListAsync(userId, activeOnly: true, cancellationToken);
        if (payments.Count == 0)
        {
            return [];
        }

        var accounts = (await _accounts.ListAsync(userId, includeArchived: true, cancellationToken))
            .ToDictionary(x => x.Id, x => x.Name);
        var handled = (await _occurrences.ListByUserAsync(userId, start, end, cancellationToken))
            .Where(x => x.Status is OccurrenceStatus.Paid or OccurrenceStatus.Skipped)
            .Select(x => (x.RecurringPaymentId, x.DueDate))
            .ToHashSet();

        var upcoming = new List<UpcomingPaymentDto>();
        foreach (var payment in payments)
        {
            foreach (var dueDate in payment.ProjectOccurrences(start, end))
            {
                if (handled.Contains((payment.Id, dueDate)))
                {
                    continue;
                }

                upcoming.Add(new UpcomingPaymentDto(
                    payment.Id,
                    payment.Description,
                    payment.Amount,
                    dueDate,
                    payment.Frequency,
                    payment.AccountId,
                    accounts.GetValueOrDefault(payment.AccountId) ?? string.Empty));
            }
        }

        return upcoming
            .OrderBy(x => x.DueDate)
            .ThenBy(x => x.Description)
            .Take(10)
            .ToList();
    }

    public async Task<IReadOnlyList<GoalProgressDto>> GetGoalsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var goals = await _goals.ListAsync(userId, null, cancellationToken);
        return goals
            .Where(x => x.Status != Domain.Goals.GoalStatus.Cancelled)
            .Select(x => new GoalProgressDto(x.Id, x.Name, x.TargetAmount, x.Accumulated, x.ProgressPercent, x.Deadline, x.Status))
            .ToList();
    }

    private (DateOnly Start, DateOnly End) ResolvePeriod(DateOnly? from, DateOnly? to)
    {
        var today = _clock.Today;
        var start = from ?? new DateOnly(today.Year, today.Month, 1);
        var end = to ?? start.AddMonths(1).AddDays(-1);

        if (end < start)
        {
            throw new ValidationAppException("period", "A data inicial deve ser anterior à data final.");
        }

        if (end.DayNumber - start.DayNumber + 1 > MaxPeriodDays)
        {
            throw new ValidationAppException("period", $"O período máximo para consulta é de {MaxPeriodDays} dias.");
        }

        return (start, end);
    }

    private static decimal PercentChange(decimal current, decimal previous)
    {
        if (previous == 0)
        {
            return current == 0 ? 0 : 100;
        }

        return Math.Round((current - previous) / Math.Abs(previous) * 100, 1);
    }
}
