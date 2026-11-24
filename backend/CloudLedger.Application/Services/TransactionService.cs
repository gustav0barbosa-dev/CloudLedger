using CloudLedger.Application.Abstractions;
using CloudLedger.Application.Abstractions.Persistence;
using CloudLedger.Application.Common;
using CloudLedger.Application.Common.Exceptions;
using CloudLedger.Application.Common.Models;
using CloudLedger.Application.Contracts.Finance;
using CloudLedger.Domain.Audit;
using CloudLedger.Domain.Categories;
using CloudLedger.Domain.Events;
using CloudLedger.Domain.Transactions;

namespace CloudLedger.Application.Services;

public sealed class TransactionService
{
    private readonly ITransactionRepository _transactions;
    private readonly IAccountRepository _accounts;
    private readonly ICategoryRepository _categories;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditWriter _audit;
    private readonly IDomainEventPublisher _events;
    private readonly ICorrelationContext _correlation;
    private readonly IClock _clock;

    public TransactionService(
        ITransactionRepository transactions,
        IAccountRepository accounts,
        ICategoryRepository categories,
        IUnitOfWork unitOfWork,
        IAuditWriter audit,
        IDomainEventPublisher events,
        ICorrelationContext correlation,
        IClock clock)
    {
        _transactions = transactions;
        _accounts = accounts;
        _categories = categories;
        _unitOfWork = unitOfWork;
        _audit = audit;
        _events = events;
        _correlation = correlation;
        _clock = clock;
    }

    public async Task<PagedResult<TransactionDto>> ListAsync(Guid userId, TransactionQuery query, CancellationToken cancellationToken = default)
    {
        if (query.From is not null && query.To is not null && query.From > query.To)
        {
            throw new ValidationAppException("period", "A data inicial deve ser anterior à data final.");
        }

        var (page, pageSize) = Paging.Normalize(query.Page, query.PageSize);
        return await _transactions.ListAsync(userId, query with { Page = page, PageSize = pageSize }, cancellationToken);
    }

    public async Task<TransactionDto> GetAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        return await _transactions.GetViewAsync(userId, id, cancellationToken)
            ?? throw new NotFoundException("Lançamento");
    }

    public async Task<TransactionDto> CreateAsync(Guid userId, CreateTransactionRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureAccountIsUsableAsync(userId, request.AccountId, cancellationToken);
        await EnsureCategoryMatchesAsync(userId, request.CategoryId, request.Type, cancellationToken);

        var transaction = new Transaction(
            userId,
            request.AccountId,
            request.CategoryId,
            request.Type,
            request.Amount,
            request.Description,
            request.OccurredAt);

        _transactions.Add(transaction);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _audit.WriteAsync(
            AuditEventFactory.Create(userId, AuditActions.TransactionCreated, nameof(Transaction), transaction.Id, _correlation.CorrelationId, _clock.UtcNow,
                ("type", transaction.Type.ToString()),
                ("amount", transaction.Amount.ToString("0.00")),
                ("accountId", transaction.AccountId.ToString()),
                ("occurredAt", transaction.OccurredAt.ToString("yyyy-MM-dd"))),
            cancellationToken);

        await _events.PublishAsync(
            new DomainEvent(Guid.NewGuid(), AuditActions.TransactionCreated, userId, transaction.Id, _clock.UtcNow,
                new Dictionary<string, string?>
                {
                    ["amount"] = transaction.Amount.ToString("0.00"),
                    ["type"] = transaction.Type.ToString(),
                    ["accountId"] = transaction.AccountId.ToString()
                }),
            cancellationToken);

        return await GetAsync(userId, transaction.Id, cancellationToken);
    }

    public async Task<TransactionDto> UpdateAsync(Guid userId, Guid id, UpdateTransactionRequest request, CancellationToken cancellationToken = default)
    {
        var transaction = await _transactions.GetAsync(userId, id, cancellationToken)
            ?? throw new NotFoundException("Lançamento");

        await EnsureAccountIsUsableAsync(userId, request.AccountId, cancellationToken);
        await EnsureCategoryMatchesAsync(userId, request.CategoryId, request.Type, cancellationToken);

        transaction.Update(
            request.AccountId,
            request.CategoryId,
            request.Type,
            request.Amount,
            request.Description,
            request.OccurredAt);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _audit.WriteAsync(
            AuditEventFactory.Create(userId, AuditActions.TransactionUpdated, nameof(Transaction), transaction.Id, _correlation.CorrelationId, _clock.UtcNow,
                ("type", transaction.Type.ToString()), ("amount", transaction.Amount.ToString("0.00"))),
            cancellationToken);

        return await GetAsync(userId, transaction.Id, cancellationToken);
    }

    public async Task VoidAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        var transaction = await _transactions.GetAsync(userId, id, cancellationToken)
            ?? throw new NotFoundException("Lançamento");

        transaction.Void(_clock.UtcNow);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _audit.WriteAsync(
            AuditEventFactory.Create(userId, AuditActions.TransactionVoided, nameof(Transaction), transaction.Id, _correlation.CorrelationId, _clock.UtcNow,
                ("amount", transaction.Amount.ToString("0.00"))),
            cancellationToken);

        await _events.PublishAsync(
            new DomainEvent(Guid.NewGuid(), AuditActions.TransactionVoided, userId, transaction.Id, _clock.UtcNow,
                new Dictionary<string, string?> { ["accountId"] = transaction.AccountId.ToString() }),
            cancellationToken);
    }

    private async Task EnsureAccountIsUsableAsync(Guid userId, Guid accountId, CancellationToken cancellationToken)
    {
        var account = await _accounts.GetAsync(userId, accountId, includeArchived: false, cancellationToken)
            ?? throw new NotFoundException("Conta");

        account.EnsureCanReceiveMovements();
    }

    private async Task EnsureCategoryMatchesAsync(Guid userId, Guid? categoryId, TransactionType type, CancellationToken cancellationToken)
    {
        if (categoryId is null)
        {
            return;
        }

        var category = await _categories.GetAsync(userId, categoryId.Value, cancellationToken)
            ?? throw new NotFoundException("Categoria");

        var expected = type == TransactionType.Income ? CategoryKind.Income : CategoryKind.Expense;
        if (category.Kind != expected)
        {
            throw new ValidationAppException("categoryId", $"A categoria selecionada deve ser do tipo {expected}.");
        }
    }
}
