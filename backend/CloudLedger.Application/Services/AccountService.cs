using CloudLedger.Application.Abstractions;
using CloudLedger.Application.Abstractions.Persistence;
using CloudLedger.Application.Common;
using CloudLedger.Application.Common.Exceptions;
using CloudLedger.Application.Contracts.Finance;
using CloudLedger.Domain.Accounts;
using CloudLedger.Domain.Audit;

namespace CloudLedger.Application.Services;

public sealed class AccountService
{
    private readonly IAccountRepository _accounts;
    private readonly IAccountBalanceReader _balances;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditWriter _audit;
    private readonly ICorrelationContext _correlation;
    private readonly IClock _clock;

    public AccountService(
        IAccountRepository accounts,
        IAccountBalanceReader balances,
        IUnitOfWork unitOfWork,
        IAuditWriter audit,
        ICorrelationContext correlation,
        IClock clock)
    {
        _accounts = accounts;
        _balances = balances;
        _unitOfWork = unitOfWork;
        _audit = audit;
        _correlation = correlation;
        _clock = clock;
    }

    public async Task<IReadOnlyList<AccountDto>> ListAsync(Guid userId, bool includeArchived, CancellationToken cancellationToken = default)
    {
        var accounts = await _accounts.ListAsync(userId, includeArchived, cancellationToken);
        var balances = await _balances.GetBalancesAsync(userId, null, cancellationToken);

        return accounts
            .Select(account => ToDto(account, balances.GetValueOrDefault(account.Id)))
            .ToList();
    }

    public async Task<AccountDto> GetAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        var account = await _accounts.GetAsync(userId, id, includeArchived: true, cancellationToken)
            ?? throw new NotFoundException("Conta");

        var balances = await _balances.GetBalancesAsync(userId, null, cancellationToken);
        return ToDto(account, balances.GetValueOrDefault(account.Id));
    }

    public async Task<AccountDto> CreateAsync(Guid userId, CreateAccountRequest request, CancellationToken cancellationToken = default)
    {
        var account = new Account(userId, request.Name, request.Type, request.OpeningBalance, request.Currency ?? Account.DefaultCurrency);
        _accounts.Add(account);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _audit.WriteAsync(
            AuditEventFactory.Create(userId, AuditActions.AccountCreated, nameof(Account), account.Id, _correlation.CorrelationId, _clock.UtcNow,
                ("name", account.Name), ("type", account.Type.ToString()), ("openingBalance", account.OpeningBalance.ToString("0.00"))),
            cancellationToken);

        return ToDto(account, account.OpeningBalance);
    }

    public async Task<AccountDto> UpdateAsync(Guid userId, Guid id, UpdateAccountRequest request, CancellationToken cancellationToken = default)
    {
        var account = await _accounts.GetAsync(userId, id, includeArchived: true, cancellationToken)
            ?? throw new NotFoundException("Conta");

        account.UpdateDetails(request.Name, request.Type, request.OpeningBalance);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _audit.WriteAsync(
            AuditEventFactory.Create(userId, AuditActions.AccountUpdated, nameof(Account), account.Id, _correlation.CorrelationId, _clock.UtcNow,
                ("name", account.Name), ("openingBalance", account.OpeningBalance.ToString("0.00"))),
            cancellationToken);

        var balances = await _balances.GetBalancesAsync(userId, null, cancellationToken);
        return ToDto(account, balances.GetValueOrDefault(account.Id));
    }

    public async Task ArchiveAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        var account = await _accounts.GetAsync(userId, id, includeArchived: true, cancellationToken)
            ?? throw new NotFoundException("Conta");

        account.Archive();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _audit.WriteAsync(
            AuditEventFactory.Create(userId, AuditActions.AccountArchived, nameof(Account), account.Id, _correlation.CorrelationId, _clock.UtcNow),
            cancellationToken);
    }

    public async Task UnarchiveAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        var account = await _accounts.GetAsync(userId, id, includeArchived: true, cancellationToken)
            ?? throw new NotFoundException("Conta");

        account.Unarchive();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _audit.WriteAsync(
            AuditEventFactory.Create(userId, AuditActions.AccountUnarchived, nameof(Account), account.Id, _correlation.CorrelationId, _clock.UtcNow),
            cancellationToken);
    }

    public async Task DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        var account = await _accounts.GetAsync(userId, id, includeArchived: true, cancellationToken)
            ?? throw new NotFoundException("Conta");

        if (await _accounts.HasMovementsAsync(userId, id, cancellationToken))
        {
            throw new ConflictException("account.has_movements", "Esta conta possui movimentações e não pode ser excluída. Arquive-a para preservar o histórico.");
        }

        _accounts.Remove(account);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _audit.WriteAsync(
            AuditEventFactory.Create(userId, AuditActions.AccountDeleted, nameof(Account), account.Id, _correlation.CorrelationId, _clock.UtcNow,
                ("name", account.Name)),
            cancellationToken);
    }

    private static AccountDto ToDto(Account account, decimal balance) => new(
        account.Id,
        account.Name,
        account.Type,
        account.Currency,
        account.OpeningBalance,
        balance,
        account.IsArchived,
        account.CreatedAt);
}
