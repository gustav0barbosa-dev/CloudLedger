using CloudLedger.Domain.Common;

namespace CloudLedger.Domain.Notifications;

public sealed class Notification : Entity
{
    private Notification()
    {
    }

    public Notification(
        Guid userId,
        string title,
        string message,
        NotificationType type,
        string? resourceType = null,
        Guid? resourceId = null)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("notification.user_required", "A notificação precisa estar vinculada a um usuário.");
        }

        UserId = userId;
        Title = Guard.NotEmpty(title, "título", 120);
        Message = Guard.NotEmpty(message, "mensagem", 500);
        Type = type;
        ResourceType = resourceType;
        ResourceId = resourceId;
        IsRead = false;
    }

    public Guid UserId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string Message { get; private set; } = string.Empty;

    public NotificationType Type { get; private set; }

    public bool IsRead { get; private set; }

    public string? ResourceType { get; private set; }

    public Guid? ResourceId { get; private set; }

    public void MarkAsRead() => IsRead = true;
}
