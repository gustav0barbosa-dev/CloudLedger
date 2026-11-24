using CloudLedger.Application.Abstractions;
using CloudLedger.Application.Abstractions.Persistence;
using CloudLedger.Application.Common;
using CloudLedger.Application.Common.Exceptions;
using CloudLedger.Application.Common.Models;
using CloudLedger.Application.Contracts.Finance;
using CloudLedger.Domain.Audit;
using CloudLedger.Domain.Events;
using CloudLedger.Domain.Transfers;

namespace CloudLedger.Application.Services;

public sealed class TransferService
{
    private readonly ITransferRepository _transfers;
    private readonly IAccountRepository _accounts;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditWriter _audit;
    private readonly IDomainEventPublisher _events;
    private readonly ICorrelationContext _correlation;
    private readonly IClock _clock;

    public TransferService(
        ITransferRepository transfers,
        IAccountRepository accounts,
        IUnitOfWork unitOfWork,
        IAuditWriter audit,
        IDomainEventPublisher events,
        ICorrelationContext correlation,
        IClock clock)
    {
        _transfers = transfers;
        _accounts = accounts;
        _unitOfWork = unitOfWork;
        _audit = audit;
        _events = events;
        _correlation = correlation;
        _clock = clock;
    }

    public async Task<PagedResult<TransferDto>> ListAsync(Guid userId, TransferQuery query, CancellationToken cancellationToken = default)
    {
        if (query.From is not null && query.To is not null && query.From > query.To)
        {
            throw new ValidationAppException("period", "A data inicial deve ser anterior à data final.");
        }

        var (page, pageSize) = Paging.Normalize(query.Page, query.PageSize);
        return await _transfers.ListAsync(userId, query with { Page = page, PageSize = pageSize }, cancellationToken);
    }

    public async Task<TransferDto> CreateAsync(Guid userId, CreateTransferRequest request, CancellationToken cancellationToken = default)
    {
        if (request.SourceAccountId == request.DestinationAccountId)
        {
            throw new ValidationAppException("destinationAccountId", "As contas de origem e destino devem ser diferentes.");
        }

        var source = await _accounts.GetAsync(userId, request.SourceAccountId, includeArchived: false, cancellationToken)
            ?? throw new NotFoundException("Conta de origem");

        var destination = await _accounts.GetAsync(userId, request.DestinationAccountId, includeArchived: false, cancellationToken)
            ?? throw new NotFoundException("Conta de destino");

        source.EnsureCanReceiveMovements();
        destination.EnsureCanReceiveMovements();

        var transfer = new Transfer(
            userId,
            request.SourceAccountId,
            request.DestinationAccountId,
            request.Amount,
            request.OccurredAt,
            request.Description);

        _transfers.Add(transfer);

        await _events.PublishAsync(
            new DomainEvent(Guid.NewGuid(), AuditActions.TransferCreated, userId, transfer.Id, _clock.UtcNow,
                new Dictionary<string, string?>
                {
                    ["amount"] = transfer.Amount.ToString("0.00"),
                    ["sourceAccountId"] = transfer.SourceAccountId.ToString(),
                    ["destinationAccountId"] = transfer.DestinationAccountId.ToString()
                }),
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _audit.WriteAsync(
            AuditEventFactory.Create(userId, AuditActions.TransferCreated, nameof(Transfer), transfer.Id, _correlation.CorrelationId, _clock.UtcNow,
                ("amount", transfer.Amount.ToString("0.00")),
                ("sourceAccountId", transfer.SourceAccountId.ToString()),
                ("destinationAccountId", transfer.DestinationAccountId.ToString())),
            cancellationToken);

        return await _transfers.GetViewAsync(userId, transfer.Id, cancellationToken)
            ?? throw new NotFoundException("Transferência");
    }

    public async Task VoidAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        var transfer = await _transfers.GetAsync(userId, id, cancellationToken)
            ?? throw new NotFoundException("Transferência");

        transfer.Void(_clock.UtcNow);

        await _events.PublishAsync(
            new DomainEvent(Guid.NewGuid(), AuditActions.TransferVoided, userId, transfer.Id, _clock.UtcNow,
                new Dictionary<string, string?> { ["amount"] = transfer.Amount.ToString("0.00") }),
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _audit.WriteAsync(
            AuditEventFactory.Create(userId, AuditActions.TransferVoided, nameof(Transfer), transfer.Id, _correlation.CorrelationId, _clock.UtcNow,
                ("amount", transfer.Amount.ToString("0.00"))),
            cancellationToken);
    }
}
