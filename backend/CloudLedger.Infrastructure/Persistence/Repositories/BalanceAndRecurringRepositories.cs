using CloudLedger.Application.Abstractions.Persistence;
using CloudLedger.Domain.Transactions;
using CloudLedger.Domain.Transfers;
using Microsoft.EntityFrameworkCore;

namespace CloudLedger.Infrastructure.Persistence.Repositories;

public sealed class AccountBalanceReader : IAccountBalanceReader
{
    private readonly AppDbContext _db;

    public AccountBalanceReader(AppDbContext db) => _db = db;

    public async Task<IReadOnlyDictionary<Guid, decimal>> GetBalancesAsync(Guid userId, DateOnly? asOf, CancellationToken cancellationToken = default)
    {
        var accounts = await _db.Accounts
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => new { x.Id, x.OpeningBalance })
            .ToListAsync(cancellationToken);

        var transactionQuery = _db.Transactions
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.Status == TransactionStatus.Confirmed);

        var transferQuery = _db.Transfers
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.Status == TransferStatus.Confirmed);

        if (asOf is not null)
        {
            transactionQuery = transactionQuery.Where(x => x.OccurredAt <= asOf);
            transferQuery = transferQuery.Where(x => x.OccurredAt <= asOf);
        }

        var incomeTotals = await transactionQuery
            .Where(x => x.Type == TransactionType.Income)
            .GroupBy(x => x.AccountId)
            .Select(g => new { AccountId = g.Key, Total = g.Sum(x => x.Amount) })
            .ToDictionaryAsync(x => x.AccountId, x => x.Total, cancellationToken);

        var expenseTotals = await transactionQuery
            .Where(x => x.Type == TransactionType.Expense)
            .GroupBy(x => x.AccountId)
            .Select(g => new { AccountId = g.Key, Total = g.Sum(x => x.Amount) })
            .ToDictionaryAsync(x => x.AccountId, x => x.Total, cancellationToken);

        var incoming = await transferQuery
            .GroupBy(x => x.DestinationAccountId)
            .Select(g => new { AccountId = g.Key, Total = g.Sum(x => x.Amount) })
            .ToDictionaryAsync(x => x.AccountId, x => x.Total, cancellationToken);

        var outgoing = await transferQuery
            .GroupBy(x => x.SourceAccountId)
            .Select(g => new { AccountId = g.Key, Total = g.Sum(x => x.Amount) })
            .ToDictionaryAsync(x => x.AccountId, x => x.Total, cancellationToken);

        var result = new Dictionary<Guid, decimal>(accounts.Count);
        foreach (var account in accounts)
        {
            result[account.Id] = account.OpeningBalance
                + incomeTotals.GetValueOrDefault(account.Id)
                - expenseTotals.GetValueOrDefault(account.Id)
                + incoming.GetValueOrDefault(account.Id)
                - outgoing.GetValueOrDefault(account.Id);
        }

        return result;
    }

    public async Task<decimal> GetTotalBalanceAsync(Guid userId, DateOnly? asOf, CancellationToken cancellationToken = default)
    {
        var balances = await GetBalancesAsync(userId, asOf, cancellationToken);
        return balances.Values.Sum();
    }
}

public sealed class RecurringPaymentRepository : IRecurringPaymentRepository
{
    private readonly AppDbContext _db;

    public RecurringPaymentRepository(AppDbContext db) => _db = db;

    public Task<Domain.Recurring.RecurringPayment?> GetAsync(Guid userId, Guid id, CancellationToken cancellationToken = default) =>
        _db.RecurringPayments.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, cancellationToken);

    public async Task<IReadOnlyList<Domain.Recurring.RecurringPayment>> ListAsync(Guid userId, bool activeOnly, CancellationToken cancellationToken = default) =>
        await _db.RecurringPayments
            .AsNoTracking()
            .Where(x => x.UserId == userId && (!activeOnly || x.IsActive))
            .OrderBy(x => x.NextDueDate)
            .ThenBy(x => x.Description)
            .ToListAsync(cancellationToken);

    public void Add(Domain.Recurring.RecurringPayment recurringPayment) => _db.RecurringPayments.Add(recurringPayment);
}

public sealed class RecurringOccurrenceRepository : IRecurringOccurrenceRepository
{
    private readonly AppDbContext _db;

    public RecurringOccurrenceRepository(AppDbContext db) => _db = db;

    public Task<Domain.Recurring.RecurringOccurrence?> GetAsync(Guid recurringPaymentId, DateOnly dueDate, CancellationToken cancellationToken = default) =>
        _db.RecurringOccurrences.FirstOrDefaultAsync(
            x => x.RecurringPaymentId == recurringPaymentId && x.DueDate == dueDate,
            cancellationToken);

    public async Task<IReadOnlyList<Domain.Recurring.RecurringOccurrence>> ListAsync(
        Guid recurringPaymentId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default) =>
        await _db.RecurringOccurrences
            .AsNoTracking()
            .Where(x => x.RecurringPaymentId == recurringPaymentId && x.DueDate >= from && x.DueDate <= to)
            .OrderBy(x => x.DueDate)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Domain.Recurring.RecurringOccurrence>> ListByUserAsync(
        Guid userId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default) =>
        await _db.RecurringOccurrences
            .AsNoTracking()
            .Join(
                _db.RecurringPayments,
                occurrence => occurrence.RecurringPaymentId,
                payment => payment.Id,
                (occurrence, payment) => new { occurrence, payment })
            .Where(x => x.payment.UserId == userId && x.occurrence.DueDate >= from && x.occurrence.DueDate <= to)
            .Select(x => x.occurrence)
            .ToListAsync(cancellationToken);

    public void Add(Domain.Recurring.RecurringOccurrence occurrence) => _db.RecurringOccurrences.Add(occurrence);
}
