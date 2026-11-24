using CloudLedger.Application.Abstractions.Persistence;
using CloudLedger.Domain.Accounts;
using CloudLedger.Domain.Categories;
using Microsoft.EntityFrameworkCore;

namespace CloudLedger.Infrastructure.Persistence.Repositories;

public sealed class AccountRepository : IAccountRepository
{
    private readonly AppDbContext _db;

    public AccountRepository(AppDbContext db) => _db = db;

    public Task<Account?> GetAsync(Guid userId, Guid id, bool includeArchived = false, CancellationToken cancellationToken = default) =>
        _db.Accounts.FirstOrDefaultAsync(
            x => x.Id == id && x.UserId == userId && (includeArchived || !x.IsArchived),
            cancellationToken);

    public async Task<IReadOnlyList<Account>> ListAsync(Guid userId, bool includeArchived, CancellationToken cancellationToken = default) =>
        await _db.Accounts
            .AsNoTracking()
            .Where(x => x.UserId == userId && (includeArchived || !x.IsArchived))
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

    public async Task<bool> HasMovementsAsync(Guid userId, Guid accountId, CancellationToken cancellationToken = default)
    {
        var hasTransactions = await _db.Transactions.AnyAsync(x => x.UserId == userId && x.AccountId == accountId, cancellationToken);
        if (hasTransactions)
        {
            return true;
        }

        var hasTransfers = await _db.Transfers.AnyAsync(
            x => x.UserId == userId && (x.SourceAccountId == accountId || x.DestinationAccountId == accountId),
            cancellationToken);
        if (hasTransfers)
        {
            return true;
        }

        var hasRecurring = await _db.RecurringPayments.AnyAsync(x => x.UserId == userId && x.AccountId == accountId, cancellationToken);
        if (hasRecurring)
        {
            return true;
        }

        return await _db.SavingGoals.AnyAsync(x => x.UserId == userId && x.AccountId == accountId, cancellationToken);
    }

    public void Add(Account account) => _db.Accounts.Add(account);

    public void Remove(Account account) => _db.Accounts.Remove(account);
}

public sealed class CategoryRepository : ICategoryRepository
{
    private readonly AppDbContext _db;

    public CategoryRepository(AppDbContext db) => _db = db;

    public Task<Category?> GetAsync(Guid userId, Guid id, CancellationToken cancellationToken = default) =>
        _db.Categories.FirstOrDefaultAsync(
            x => x.Id == id && (x.UserId == null || x.UserId == userId),
            cancellationToken);

    public async Task<IReadOnlyList<Category>> ListAsync(Guid userId, CategoryKind? kind, CancellationToken cancellationToken = default)
    {
        var query = _db.Categories.AsNoTracking()
            .Where(x => x.UserId == null || x.UserId == userId);

        if (kind is not null)
        {
            query = query.Where(x => x.Kind == kind);
        }

        return await query
            .OrderBy(x => x.Kind)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> IsInUseAsync(Guid userId, Guid categoryId, CancellationToken cancellationToken = default)
    {
        var usedByTransaction = await _db.Transactions.AnyAsync(
            x => x.UserId == userId && x.CategoryId == categoryId,
            cancellationToken);

        return usedByTransaction || await _db.RecurringPayments.AnyAsync(
            x => x.UserId == userId && x.CategoryId == categoryId,
            cancellationToken);
    }

    public void Add(Category category) => _db.Categories.Add(category);

    public void Remove(Category category) => _db.Categories.Remove(category);
}
