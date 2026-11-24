using System.Security.Cryptography;
using System.Text;
using CloudLedger.Application.Abstractions;
using CloudLedger.Application.Abstractions.Persistence;
using CloudLedger.Application.Common;
using CloudLedger.Application.Common.Exceptions;
using CloudLedger.Application.Contracts.Planning;
using CloudLedger.Domain.Audit;
using CloudLedger.Domain.Categories;
using CloudLedger.Domain.Events;
using CloudLedger.Domain.Notifications;
using CloudLedger.Domain.Recurring;
using CloudLedger.Domain.Transactions;

namespace CloudLedger.Application.Services;

public sealed class RecurringPaymentService
{
    private readonly IRecurringPaymentRepository _recurring;
    private readonly IRecurringOccurrenceRepository _occurrences;
    private readonly ITransactionRepository _transactions;
    private readonly IAccountRepository _accounts;
    private readonly ICategoryRepository _categories;
    private readonly INotificationRepository _notifications;
    private readonly IIdempotencyStore _idempotency;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditWriter _audit;
    private readonly IDomainEventPublisher _events;
    private readonly ICorrelationContext _correlation;
    private readonly IClock _clock;

    public RecurringPaymentService(
        IRecurringPaymentRepository recurring,
        IRecurringOccurrenceRepository occurrences,
        ITransactionRepository transactions,
        IAccountRepository accounts,
        ICategoryRepository categories,
        INotificationRepository notifications,
        IIdempotencyStore idempotency,
        IUnitOfWork unitOfWork,
        IAuditWriter audit,
        IDomainEventPublisher events,
        ICorrelationContext correlation,
        IClock clock)
    {
        _recurring = recurring;
        _occurrences = occurrences;
        _transactions = transactions;
        _accounts = accounts;
        _categories = categories;
        _notifications = notifications;
        _idempotency = idempotency;
        _unitOfWork = unitOfWork;
        _audit = audit;
        _events = events;
        _correlation = correlation;
        _clock = clock;
    }

    public async Task<IReadOnlyList<RecurringPaymentDto>> ListAsync(Guid userId, bool activeOnly, CancellationToken cancellationToken = default)
    {
        var payments = await _recurring.ListAsync(userId, activeOnly, cancellationToken);
        if (payments.Count == 0)
        {
            return [];
        }

        var accounts = (await _accounts.ListAsync(userId, includeArchived: true, cancellationToken))
            .ToDictionary(x => x.Id, x => x.Name);
        var categories = (await _categories.ListAsync(userId, null, cancellationToken))
            .ToDictionary(x => x.Id, x => x.Name);

        return payments
            .Select(payment => ToDto(payment, accounts.GetValueOrDefault(payment.AccountId) ?? string.Empty, payment.CategoryId is null ? null : categories.GetValueOrDefault(payment.CategoryId.Value)))
            .ToList();
    }

    public async Task<RecurringPaymentDto> CreateAsync(Guid userId, CreateRecurringPaymentRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureAccountIsUsableAsync(userId, request.AccountId, cancellationToken);
        await EnsureExpenseCategoryAsync(userId, request.CategoryId, cancellationToken);

        var payment = new RecurringPayment(
            userId,
            request.AccountId,
            request.CategoryId,
            request.Description,
            request.Amount,
            request.Frequency,
            request.NextDueDate);

        _recurring.Add(payment);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _audit.WriteAsync(
            AuditEventFactory.Create(userId, AuditActions.RecurringPaymentCreated, nameof(RecurringPayment), payment.Id, _correlation.CorrelationId, _clock.UtcNow,
                ("description", payment.Description), ("amount", payment.Amount.ToString("0.00")), ("nextDueDate", payment.NextDueDate.ToString("yyyy-MM-dd"))),
            cancellationToken);

        return await GetDtoAsync(userId, payment, cancellationToken);
    }

    public async Task<RecurringPaymentDto> UpdateAsync(Guid userId, Guid id, UpdateRecurringPaymentRequest request, CancellationToken cancellationToken = default)
    {
        var payment = await _recurring.GetAsync(userId, id, cancellationToken)
            ?? throw new NotFoundException("Pagamento recorrente");

        await EnsureAccountIsUsableAsync(userId, request.AccountId, cancellationToken);
        await EnsureExpenseCategoryAsync(userId, request.CategoryId, cancellationToken);

        payment.Update(request.AccountId, request.CategoryId, request.Description, request.Amount, request.Frequency, request.NextDueDate);
        if (request.IsActive)
        {
            payment.Activate();
        }
        else
        {
            payment.Deactivate();
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _audit.WriteAsync(
            AuditEventFactory.Create(userId, AuditActions.RecurringPaymentUpdated, nameof(RecurringPayment), payment.Id, _correlation.CorrelationId, _clock.UtcNow,
                ("description", payment.Description), ("isActive", payment.IsActive.ToString())),
            cancellationToken);

        return await GetDtoAsync(userId, payment, cancellationToken);
    }

    public async Task<IReadOnlyList<OccurrenceDto>> GetOccurrencesAsync(
        Guid userId,
        Guid id,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        if (to < from)
        {
            throw new ValidationAppException("period", "A data inicial deve ser anterior à data final.");
        }

        var payment = await _recurring.GetAsync(userId, id, cancellationToken)
            ?? throw new NotFoundException("Pagamento recorrente");

        var projected = payment.ProjectOccurrences(from, to);
        var persisted = (await _occurrences.ListAsync(payment.Id, from, to, cancellationToken))
            .ToDictionary(x => x.DueDate);

        return projected
            .Select(date =>
            {
                var occurrence = persisted.GetValueOrDefault(date);
                return occurrence is null
                    ? new OccurrenceDto(payment.Id, date, OccurrenceStatus.Pending, null, null)
                    : new OccurrenceDto(payment.Id, date, occurrence.Status, occurrence.TransactionId, occurrence.PaidAt);
            })
            .ToList();
    }

    /// <summary>
    /// RN-008/RN-009/RN-012: pagar uma ocorrência gera exatamente um lançamento de despesa.
    /// A operação é protegida por chave de idempotência e pelo índice único no banco.
    /// </summary>
    public async Task<OccurrenceDto> PayOccurrenceAsync(Guid userId, Guid id, DateOnly dueDate, CancellationToken cancellationToken = default)
    {
        var payment = await _recurring.GetAsync(userId, id, cancellationToken)
            ?? throw new NotFoundException("Pagamento recorrente");

        if (!payment.IsActive)
        {
            throw new ValidationAppException("recurringPayment", "Ative o pagamento recorrente antes de registrar ocorrências.");
        }

        var existing = await _occurrences.GetAsync(payment.Id, dueDate, cancellationToken);
        if (existing?.Status == OccurrenceStatus.Paid)
        {
            throw new ConflictException("occurrence.already_paid", "Esta ocorrência já foi paga.");
        }

        // Idempotência: mesmo após o pagamento avançar NextDueDate, a data continua
        // sendo uma ocorrência válida para fins de consulta/repetição.
        if (existing is null && !payment.IsDueDate(dueDate))
        {
            throw new ValidationAppException("date", "A data informada não corresponde a uma ocorrência deste pagamento.");
        }

        var key = IdempotencyKey(payment.Id, dueDate);
        var requestHash = Hash($"{payment.Id:N}|{dueDate:yyyy-MM-dd}|{payment.Amount:0.00}");
        var started = await _idempotency.TryBeginAsync(key, requestHash, TimeSpan.FromDays(30), cancellationToken);
        if (!started)
        {
            throw new ConflictException("occurrence.duplicate", "Esta ocorrência já está sendo processada.");
        }

        try
        {
            var transaction = new Transaction(
                userId,
                payment.AccountId,
                payment.CategoryId,
                TransactionType.Expense,
                payment.Amount,
                $"Pagamento recorrente: {payment.Description}",
                dueDate);

            _transactions.Add(transaction);

            if (existing is null)
            {
                existing = new RecurringOccurrence(payment.Id, dueDate);
                _occurrences.Add(existing);
            }

            existing.MarkAsPaid(transaction.Id, _clock.UtcNow);
            payment.AdvanceAfter(dueDate);

            _notifications.Add(new Notification(
                userId,
                "Pagamento registrado",
                $"{payment.Description} — {payment.Amount:C} em {dueDate:dd/MM/yyyy}.",
                NotificationType.Info,
                nameof(RecurringPayment),
                payment.Id));

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _idempotency.CompleteAsync(key, transaction.Id.ToString(), cancellationToken);

            await _audit.WriteAsync(
                AuditEventFactory.Create(userId, AuditActions.RecurringOccurrencePaid, nameof(RecurringPayment), payment.Id, _correlation.CorrelationId, _clock.UtcNow,
                    ("dueDate", dueDate.ToString("yyyy-MM-dd")), ("transactionId", transaction.Id.ToString())),
                cancellationToken);

            await _events.PublishAsync(
                new DomainEvent(Guid.NewGuid(), AuditActions.RecurringOccurrencePaid, userId, payment.Id, _clock.UtcNow,
                    new Dictionary<string, string?>
                    {
                        ["dueDate"] = dueDate.ToString("yyyy-MM-dd"),
                        ["transactionId"] = transaction.Id.ToString()
                    }),
                cancellationToken);

            return new OccurrenceDto(payment.Id, dueDate, OccurrenceStatus.Paid, transaction.Id, existing.PaidAt);
        }
        catch
        {
            await _idempotency.FailAsync(key, cancellationToken);
            throw;
        }
    }

    public async Task SkipOccurrenceAsync(Guid userId, Guid id, DateOnly dueDate, CancellationToken cancellationToken = default)
    {
        var payment = await _recurring.GetAsync(userId, id, cancellationToken)
            ?? throw new NotFoundException("Pagamento recorrente");

        if (!payment.IsDueDate(dueDate))
        {
            throw new ValidationAppException("date", "A data informada não corresponde a uma ocorrência deste pagamento.");
        }

        var occurrence = await _occurrences.GetAsync(payment.Id, dueDate, cancellationToken);
        if (occurrence?.Status == OccurrenceStatus.Paid)
        {
            throw new ConflictException("occurrence.already_paid", "Ocorrências pagas não podem ser ignoradas.");
        }

        if (occurrence is null)
        {
            occurrence = new RecurringOccurrence(payment.Id, dueDate);
            _occurrences.Add(occurrence);
        }

        occurrence.Skip();
        payment.AdvanceAfter(dueDate);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _audit.WriteAsync(
            AuditEventFactory.Create(userId, AuditActions.RecurringOccurrenceSkipped, nameof(RecurringPayment), payment.Id, _correlation.CorrelationId, _clock.UtcNow,
                ("dueDate", dueDate.ToString("yyyy-MM-dd"))),
            cancellationToken);
    }

    private async Task<RecurringPaymentDto> GetDtoAsync(Guid userId, RecurringPayment payment, CancellationToken cancellationToken)
    {
        var account = await _accounts.GetAsync(userId, payment.AccountId, includeArchived: true, cancellationToken);
        string? categoryName = null;
        if (payment.CategoryId is not null)
        {
            var category = await _categories.GetAsync(userId, payment.CategoryId.Value, cancellationToken);
            categoryName = category?.Name;
        }

        return ToDto(payment, account?.Name ?? string.Empty, categoryName);
    }

    private async Task EnsureAccountIsUsableAsync(Guid userId, Guid accountId, CancellationToken cancellationToken)
    {
        var account = await _accounts.GetAsync(userId, accountId, includeArchived: false, cancellationToken)
            ?? throw new NotFoundException("Conta");

        account.EnsureCanReceiveMovements();
    }

    private async Task EnsureExpenseCategoryAsync(Guid userId, Guid? categoryId, CancellationToken cancellationToken)
    {
        if (categoryId is null)
        {
            return;
        }

        var category = await _categories.GetAsync(userId, categoryId.Value, cancellationToken)
            ?? throw new NotFoundException("Categoria");

        if (category.Kind != CategoryKind.Expense)
        {
            throw new ValidationAppException("categoryId", "Pagamentos recorrentes exigem uma categoria de despesa.");
        }
    }

    private static RecurringPaymentDto ToDto(RecurringPayment payment, string accountName, string? categoryName) => new(
        payment.Id,
        payment.AccountId,
        accountName,
        payment.CategoryId,
        categoryName,
        payment.Description,
        payment.Amount,
        payment.Frequency,
        payment.NextDueDate,
        payment.IsActive,
        payment.CreatedAt);

    private static string IdempotencyKey(Guid paymentId, DateOnly dueDate) => $"recurring:{paymentId:N}:{dueDate:yyyy-MM-dd}";

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
