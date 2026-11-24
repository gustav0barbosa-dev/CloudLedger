namespace CloudLedger.Domain.Audit;

/// <summary>
/// Nomes canônicos de ações auditadas. Mantidos como constantes para evitar
/// divergência entre produtores (API/Worker) e consumidores (consulta).
/// </summary>
public static class AuditActions
{
    public const string UserRegistered = "UserRegistered";
    public const string UserProfileUpdated = "UserProfileUpdated";
    public const string UserPasswordChanged = "UserPasswordChanged";
    public const string UserLoggedIn = "UserLoggedIn";
    public const string UserLoggedOut = "UserLoggedOut";

    public const string AccountCreated = "AccountCreated";
    public const string AccountUpdated = "AccountUpdated";
    public const string AccountArchived = "AccountArchived";
    public const string AccountUnarchived = "AccountUnarchived";
    public const string AccountDeleted = "AccountDeleted";

    public const string CategoryCreated = "CategoryCreated";
    public const string CategoryUpdated = "CategoryUpdated";
    public const string CategoryDeleted = "CategoryDeleted";

    public const string TransactionCreated = "TransactionCreated";
    public const string TransactionUpdated = "TransactionUpdated";
    public const string TransactionVoided = "TransactionVoided";

    public const string TransferCreated = "TransferCreated";
    public const string TransferVoided = "TransferVoided";

    public const string RecurringPaymentCreated = "RecurringPaymentCreated";
    public const string RecurringPaymentUpdated = "RecurringPaymentUpdated";
    public const string RecurringOccurrencePaid = "RecurringOccurrencePaid";
    public const string RecurringOccurrenceSkipped = "RecurringOccurrenceSkipped";

    public const string GoalCreated = "GoalCreated";
    public const string GoalUpdated = "GoalUpdated";
    public const string GoalCancelled = "GoalCancelled";
    public const string GoalMovementCreated = "GoalMovementCreated";

    public const string ReportExported = "ReportExported";
}
