using CloudLedger.Application.Abstractions.Persistence;
using CloudLedger.Application.Common.Models;
using CloudLedger.Application.Contracts.Finance;
using CloudLedger.Domain.Transactions;
using CloudLedger.Domain.Transfers;
using Microsoft.EntityFrameworkCore;

namespace CloudLedger.Infrastructure.Persistence.Repositories;

public sealed class TransactionRepository : ITransactionRepository
{
    private readonly AppDbContext _db;

    public TransactionRepository(AppDbContext db) => _db = db;

    public Task<Transaction?> GetAsync(Guid userId, Guid id, CancellationToken cancellationToken = default) =>
        _db.Transactions.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, cancellationToken);

    public Task<TransactionDto?> GetViewAsync(Guid userId, Guid id, CancellationToken cancellationToken = default) =>
        (from t in _db.Transactions.AsNoTracking()
         join a in _db.Accounts on t.AccountId equals a.Id
         join c in _db.Categories on t.CategoryId equals c.Id into categories
         from category in categories.DefaultIfEmpty()
         where t.Id == id && t.UserId == userId
         select new TransactionDto(
             t.Id,
             t.AccountId,
             a.Name,
             t.CategoryId,
             category == null ? null : category.Name,
             t.Type,
             t.Amount,
             t.Description,
             t.OccurredAt,
             t.Status,
             t.CreatedAt,
             t.VoidedAt))
        .FirstOrDefaultAsync(cancellationToken);

    public async Task<PagedResult<TransactionDto>> ListAsync(Guid userId, TransactionQuery query, CancellationToken cancellationToken = default)
    {
        var baseQuery = _db.Transactions.AsNoTracking().Where(x => x.UserId == userId);

        if (query.From is not null)
        {
            baseQuery = baseQuery.Where(x => x.OccurredAt >= query.From);
        }

        if (query.To is not null)
        {
            baseQuery = baseQuery.Where(x => x.OccurredAt <= query.To);
        }

        if (query.Type is not null)
        {
            baseQuery = baseQuery.Where(x => x.Type == query.Type);
        }

        if (query.CategoryId is not null)
        {
            baseQuery = baseQuery.Where(x => x.CategoryId == query.CategoryId);
        }

        if (query.AccountId is not null)
        {
            baseQuery = baseQuery.Where(x => x.AccountId == query.AccountId);
        }

        if (query.Status is not null)
        {
            baseQuery = baseQuery.Where(x => x.Status == query.Status);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLowerInvariant();
            baseQuery = baseQuery.Where(x => x.Description.ToLower().Contains(term));
        }

        var total = await baseQuery.CountAsync(cancellationToken);
        var ordered = ApplySort(baseQuery, query.SortBy, query.SortDirection);

        var items = await (from t in ordered
                           join a in _db.Accounts on t.AccountId equals a.Id
                           join c in _db.Categories on t.CategoryId equals c.Id into categories
                           from category in categories.DefaultIfEmpty()
                           select new TransactionDto(
                               t.Id,
                               t.AccountId,
                               a.Name,
                               t.CategoryId,
                               category == null ? null : category.Name,
                               t.Type,
                               t.Amount,
                               t.Description,
                               t.OccurredAt,
                               t.Status,
                               t.CreatedAt,
                               t.VoidedAt))
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<TransactionDto>(items, query.Page, query.PageSize, total);
    }

    public async Task<IReadOnlyList<TransactionDto>> ListRecentAsync(Guid userId, int take, CancellationToken cancellationToken = default) =>
        await (from t in _db.Transactions.AsNoTracking()
               join a in _db.Accounts on t.AccountId equals a.Id
               join c in _db.Categories on t.CategoryId equals c.Id into categories
               from category in categories.DefaultIfEmpty()
               where t.UserId == userId
               orderby t.OccurredAt descending, t.CreatedAt descending
               select new TransactionDto(
                   t.Id,
                   t.AccountId,
                   a.Name,
                   t.CategoryId,
                   category == null ? null : category.Name,
                   t.Type,
                   t.Amount,
                   t.Description,
                   t.OccurredAt,
                   t.Status,
                   t.CreatedAt,
                   t.VoidedAt))
            .Take(take)
            .ToListAsync(cancellationToken);

    public async Task<decimal> SumAsync(Guid userId, TransactionType type, DateOnly from, DateOnly to, CancellationToken cancellationToken = default) =>
        await _db.Transactions
            .AsNoTracking()
            .Where(x => x.UserId == userId
                        && x.Status == TransactionStatus.Confirmed
                        && x.Type == type
                        && x.OccurredAt >= from
                        && x.OccurredAt <= to)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;

    public async Task<IReadOnlyList<CategoryTotal>> SumByCategoryAsync(Guid userId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        var grouped = await _db.Transactions
            .AsNoTracking()
            .Where(x => x.UserId == userId
                        && x.Status == TransactionStatus.Confirmed
                        && x.Type == TransactionType.Expense
                        && x.OccurredAt >= from
                        && x.OccurredAt <= to)
            .GroupBy(x => x.CategoryId)
            .Select(g => new { CategoryId = g.Key, Total = g.Sum(x => x.Amount), Count = g.Count() })
            .ToListAsync(cancellationToken);

        var ids = grouped.Where(x => x.CategoryId is not null).Select(x => x.CategoryId!.Value).ToList();
        var categories = await _db.Categories
            .AsNoTracking()
            .Where(x => ids.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        return grouped
            .Select(g =>
            {
                var category = g.CategoryId is null ? null : categories.GetValueOrDefault(g.CategoryId.Value);
                return new CategoryTotal(
                    g.CategoryId,
                    category?.Name ?? "Sem categoria",
                    category?.Color,
                    g.Total,
                    g.Count);
            })
            .OrderByDescending(x => x.Total)
            .ToList();
    }

    public async Task<IReadOnlyList<DayTotal>> SumByDayAsync(Guid userId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        // A agregação condicional (Sum com ternário) não é traduzível em todos os
        // provedores (InMemory/Postgres), então a agrupamento é feito em memória.
        var rows = await _db.Transactions
            .AsNoTracking()
            .Where(x => x.UserId == userId
                        && x.Status == TransactionStatus.Confirmed
                        && x.OccurredAt >= from
                        && x.OccurredAt <= to)
            .Select(x => new { x.OccurredAt, x.Type, x.Amount })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(x => x.OccurredAt)
            .Select(g => new DayTotal(
                g.Key,
                g.Where(x => x.Type == TransactionType.Income).Sum(x => x.Amount),
                g.Where(x => x.Type == TransactionType.Expense).Sum(x => x.Amount)))
            .OrderBy(x => x.Date)
            .ToList();
    }

    public Task<bool> AnyForAccountAsync(Guid userId, Guid accountId, CancellationToken cancellationToken = default) =>
        _db.Transactions.AnyAsync(x => x.UserId == userId && x.AccountId == accountId, cancellationToken);

    public Task<bool> AnyForCategoryAsync(Guid userId, Guid categoryId, CancellationToken cancellationToken = default) =>
        _db.Transactions.AnyAsync(x => x.UserId == userId && x.CategoryId == categoryId, cancellationToken);

    public void Add(Transaction transaction) => _db.Transactions.Add(transaction);

    private static IQueryable<Transaction> ApplySort(IQueryable<Transaction> query, string? sortBy, string? sortDirection)
    {
        var descending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);

        return sortBy?.Trim().ToLowerInvariant() switch
        {
            "amount" => descending ? query.OrderByDescending(x => x.Amount) : query.OrderBy(x => x.Amount),
            "description" => descending ? query.OrderByDescending(x => x.Description) : query.OrderBy(x => x.Description),
            "createdat" => descending ? query.OrderByDescending(x => x.CreatedAt) : query.OrderBy(x => x.CreatedAt),
            _ => descending
                ? query.OrderByDescending(x => x.OccurredAt).ThenByDescending(x => x.CreatedAt)
                : query.OrderBy(x => x.OccurredAt).ThenBy(x => x.CreatedAt)
        };
    }
}

public sealed class TransferRepository : ITransferRepository
{
    private readonly AppDbContext _db;

    public TransferRepository(AppDbContext db) => _db = db;

    public Task<Transfer?> GetAsync(Guid userId, Guid id, CancellationToken cancellationToken = default) =>
        _db.Transfers.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, cancellationToken);

    public Task<TransferDto?> GetViewAsync(Guid userId, Guid id, CancellationToken cancellationToken = default) =>
        (from t in _db.Transfers.AsNoTracking()
         join source in _db.Accounts on t.SourceAccountId equals source.Id
         join destination in _db.Accounts on t.DestinationAccountId equals destination.Id
         where t.Id == id && t.UserId == userId
         select new TransferDto(
             t.Id,
             t.SourceAccountId,
             source.Name,
             t.DestinationAccountId,
             destination.Name,
             t.Amount,
             t.OccurredAt,
             t.Description,
             t.Status,
             t.CreatedAt,
             t.VoidedAt))
        .FirstOrDefaultAsync(cancellationToken);

    public async Task<PagedResult<TransferDto>> ListAsync(Guid userId, TransferQuery query, CancellationToken cancellationToken = default)
    {
        var baseQuery = _db.Transfers.AsNoTracking().Where(x => x.UserId == userId);

        if (query.From is not null)
        {
            baseQuery = baseQuery.Where(x => x.OccurredAt >= query.From);
        }

        if (query.To is not null)
        {
            baseQuery = baseQuery.Where(x => x.OccurredAt <= query.To);
        }

        if (query.AccountId is not null)
        {
            baseQuery = baseQuery.Where(x => x.SourceAccountId == query.AccountId || x.DestinationAccountId == query.AccountId);
        }

        var total = await baseQuery.CountAsync(cancellationToken);

        var items = await (from t in baseQuery
                           join source in _db.Accounts on t.SourceAccountId equals source.Id
                           join destination in _db.Accounts on t.DestinationAccountId equals destination.Id
                           orderby t.OccurredAt descending, t.CreatedAt descending
                           select new TransferDto(
                               t.Id,
                               t.SourceAccountId,
                               source.Name,
                               t.DestinationAccountId,
                               destination.Name,
                               t.Amount,
                               t.OccurredAt,
                               t.Description,
                               t.Status,
                               t.CreatedAt,
                               t.VoidedAt))
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<TransferDto>(items, query.Page, query.PageSize, total);
    }

    public Task<bool> AnyForAccountAsync(Guid userId, Guid accountId, CancellationToken cancellationToken = default) =>
        _db.Transfers.AnyAsync(
            x => x.UserId == userId && (x.SourceAccountId == accountId || x.DestinationAccountId == accountId),
            cancellationToken);

    public void Add(Transfer transfer) => _db.Transfers.Add(transfer);
}
