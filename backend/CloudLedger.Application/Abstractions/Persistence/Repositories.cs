using CloudLedger.Application.Common.Models;
using CloudLedger.Domain.Accounts;
using CloudLedger.Domain.Categories;
using CloudLedger.Domain.Goals;
using CloudLedger.Domain.Notifications;
using CloudLedger.Domain.Recurring;
using CloudLedger.Domain.Security;
using CloudLedger.Domain.Transactions;
using CloudLedger.Domain.Transfers;
using CloudLedger.Domain.Users;

namespace CloudLedger.Application.Abstractions.Persistence;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);

    void Add(User user);
}

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    Task RevokeAllAsync(Guid userId, DateTime utcNow, CancellationToken cancellationToken = default);

    void Add(RefreshToken refreshToken);
}

public interface IAccountRepository
{
    Task<Account?> GetAsync(Guid userId, Guid id, bool includeArchived = false, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Account>> ListAsync(Guid userId, bool includeArchived, CancellationToken cancellationToken = default);

    Task<bool> HasMovementsAsync(Guid userId, Guid accountId, CancellationToken cancellationToken = default);

    void Add(Account account);

    void Remove(Account account);
}

public interface ICategoryRepository
{
    Task<Category?> GetAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Category>> ListAsync(Guid userId, CategoryKind? kind, CancellationToken cancellationToken = default);

    Task<bool> IsInUseAsync(Guid userId, Guid categoryId, CancellationToken cancellationToken = default);

    void Add(Category category);

    void Remove(Category category);
}

public interface ITransactionRepository
{
    Task<Transaction?> GetAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);

    Task<Contracts.Finance.TransactionDto?> GetViewAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);

    Task<PagedResult<Contracts.Finance.TransactionDto>> ListAsync(Guid userId, TransactionQuery query, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Contracts.Finance.TransactionDto>> ListRecentAsync(Guid userId, int take, CancellationToken cancellationToken = default);

    Task<decimal> SumAsync(Guid userId, TransactionType type, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CategoryTotal>> SumByCategoryAsync(Guid userId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DayTotal>> SumByDayAsync(Guid userId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);

    Task<bool> AnyForAccountAsync(Guid userId, Guid accountId, CancellationToken cancellationToken = default);

    Task<bool> AnyForCategoryAsync(Guid userId, Guid categoryId, CancellationToken cancellationToken = default);

    void Add(Transaction transaction);
}

public interface ITransferRepository
{
    Task<Transfer?> GetAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);

    Task<Contracts.Finance.TransferDto?> GetViewAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);

    Task<PagedResult<Contracts.Finance.TransferDto>> ListAsync(Guid userId, TransferQuery query, CancellationToken cancellationToken = default);

    Task<bool> AnyForAccountAsync(Guid userId, Guid accountId, CancellationToken cancellationToken = default);

    void Add(Transfer transfer);
}

public interface IAccountBalanceReader
{
    Task<IReadOnlyDictionary<Guid, decimal>> GetBalancesAsync(Guid userId, DateOnly? asOf, CancellationToken cancellationToken = default);

    Task<decimal> GetTotalBalanceAsync(Guid userId, DateOnly? asOf, CancellationToken cancellationToken = default);
}

public interface IRecurringPaymentRepository
{
    Task<RecurringPayment?> GetAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RecurringPayment>> ListAsync(Guid userId, bool activeOnly, CancellationToken cancellationToken = default);

    void Add(RecurringPayment recurringPayment);
}

public interface IRecurringOccurrenceRepository
{
    Task<RecurringOccurrence?> GetAsync(Guid recurringPaymentId, DateOnly dueDate, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RecurringOccurrence>> ListAsync(Guid recurringPaymentId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RecurringOccurrence>> ListByUserAsync(Guid userId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);

    void Add(RecurringOccurrence occurrence);
}

public interface ISavingGoalRepository
{
    Task<SavingGoal?> GetAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SavingGoal>> ListAsync(Guid userId, GoalStatus? status, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, GoalTotals>> GetTotalsAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<GoalTotals> GetGoalTotalsAsync(Guid goalId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GoalMovement>> ListMovementsAsync(Guid goalId, CancellationToken cancellationToken = default);

    void Add(SavingGoal goal);

    void AddMovement(GoalMovement movement);
}

public interface INotificationRepository
{
    Task<Notification?> GetAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);

    Task<PagedResult<Notification>> ListAsync(Guid userId, bool unreadOnly, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<int> CountUnreadAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Notification>> ListUnreadAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<bool> ExistsRecentAsync(Guid userId, Domain.Notifications.NotificationType type, Guid resourceId, DateTime sinceUtc, CancellationToken cancellationToken = default);

    Task<int> MarkAllReadAsync(Guid userId, CancellationToken cancellationToken = default);

    void Add(Notification notification);
}
