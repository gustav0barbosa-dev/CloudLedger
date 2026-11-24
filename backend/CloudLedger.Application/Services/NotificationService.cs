using CloudLedger.Application.Abstractions;
using CloudLedger.Application.Abstractions.Persistence;
using CloudLedger.Application.Common.Exceptions;
using CloudLedger.Application.Common.Models;
using CloudLedger.Application.Contracts.Dashboard;
using CloudLedger.Domain.Notifications;
using CloudLedger.Domain.Recurring;

namespace CloudLedger.Application.Services;

public sealed class NotificationService
{
    private readonly INotificationRepository _notifications;
    private readonly IRecurringPaymentRepository _recurring;
    private readonly IRecurringOccurrenceRepository _occurrences;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public NotificationService(
        INotificationRepository notifications,
        IRecurringPaymentRepository recurring,
        IRecurringOccurrenceRepository occurrences,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _notifications = notifications;
        _recurring = recurring;
        _occurrences = occurrences;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<PagedResult<NotificationDto>> ListAsync(
        Guid userId,
        bool unreadOnly,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var (normalizedPage, normalizedSize) = Paging.Normalize(page, pageSize);
        var result = await _notifications.ListAsync(userId, unreadOnly, normalizedPage, normalizedSize, cancellationToken);

        return new PagedResult<NotificationDto>(
            result.Items.Select(ToDto).ToList(),
            result.Page,
            result.PageSize,
            result.TotalItems);
    }

    public Task<int> CountUnreadAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _notifications.CountUnreadAsync(userId, cancellationToken);

    public async Task MarkAsReadAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        var notification = await _notifications.GetAsync(userId, id, cancellationToken)
            ?? throw new NotFoundException("Notificação");

        notification.MarkAsRead();
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> MarkAllAsReadAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var updated = await _notifications.MarkAllReadAsync(userId, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return updated;
    }

    /// <summary>
    /// Gera notificações de pagamentos nos próximos <paramref name="days"/> dias.
    /// Evita duplicidade verificando notificações do mesmo tipo/recurso nas últimas 24h.
    /// Chamado pelo Worker diariamente.
    /// </summary>
    public async Task<int> GenerateUpcomingAsync(Guid userId, int days = 3, CancellationToken cancellationToken = default)
    {
        var today = _clock.Today;
        var limit = today.AddDays(days);
        var payments = await _recurring.ListAsync(userId, activeOnly: true, cancellationToken);
        if (payments.Count == 0)
        {
            return 0;
        }

        var handled = (await _occurrences.ListByUserAsync(userId, today, limit, cancellationToken))
            .Where(x => x.Status is OccurrenceStatus.Paid or OccurrenceStatus.Skipped)
            .Select(x => (x.RecurringPaymentId, x.DueDate))
            .ToHashSet();

        var created = 0;
        foreach (var payment in payments)
        {
            foreach (var dueDate in payment.ProjectOccurrences(today, limit))
            {
                if (handled.Contains((payment.Id, dueDate)))
                {
                    continue;
                }

                if (await _notifications.ExistsRecentAsync(userId, NotificationType.UpcomingPayment, payment.Id, _clock.UtcNow.AddHours(-24), cancellationToken))
                {
                    continue;
                }

                _notifications.Add(new Notification(
                    userId,
                    "Pagamento próximo",
                    $"{payment.Description} vence em {dueDate:dd/MM/yyyy} ({payment.Amount:C}).",
                    NotificationType.UpcomingPayment,
                    nameof(RecurringPayment),
                    payment.Id));
                created++;
            }
        }

        if (created > 0)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return created;
    }

    private static NotificationDto ToDto(Notification notification) => new(
        notification.Id,
        notification.Title,
        notification.Message,
        notification.Type,
        notification.IsRead,
        notification.ResourceType,
        notification.ResourceId,
        notification.CreatedAt);
}
