using CloudLedger.Application.Abstractions;
using CloudLedger.Domain.Accounts;
using CloudLedger.Domain.Categories;
using CloudLedger.Domain.Goals;
using CloudLedger.Domain.Notifications;
using CloudLedger.Domain.Recurring;
using CloudLedger.Domain.Security;
using CloudLedger.Domain.Transactions;
using CloudLedger.Domain.Transfers;
using CloudLedger.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CloudLedger.Infrastructure.Persistence;

public sealed class AppDbContext : DbContext, IUnitOfWork
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();

    public DbSet<Account> Accounts => Set<Account>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Transaction> Transactions => Set<Transaction>();

    public DbSet<Transfer> Transfers => Set<Transfer>();

    public DbSet<RecurringPayment> RecurringPayments => Set<RecurringPayment>();

    public DbSet<RecurringOccurrence> RecurringOccurrences => Set<RecurringOccurrence>();

    public DbSet<SavingGoal> SavingGoals => Set<SavingGoal>();

    public DbSet<GoalMovement> GoalMovements => Set<GoalMovement>();

    public DbSet<Notification> Notifications => Set<Notification>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
    {
        if (Database.CurrentTransaction is not null || !Database.IsRelational())
        {
            await action(cancellationToken);
            return;
        }

        await using IDbContextTransaction transaction = await Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await action(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
