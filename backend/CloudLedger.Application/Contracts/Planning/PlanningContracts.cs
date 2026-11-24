using System.ComponentModel.DataAnnotations;
using CloudLedger.Domain.Goals;
using CloudLedger.Domain.Recurring;

namespace CloudLedger.Application.Contracts.Planning;

public sealed record RecurringPaymentDto(
    Guid Id,
    Guid AccountId,
    string AccountName,
    Guid? CategoryId,
    string? CategoryName,
    string Description,
    decimal Amount,
    RecurrenceFrequency Frequency,
    DateOnly NextDueDate,
    bool IsActive,
    DateTime CreatedAt);

public sealed record CreateRecurringPaymentRequest(
    Guid AccountId,
    Guid? CategoryId,
    [Required(ErrorMessage = "Informe a descrição."), MaxLength(200)] string Description,
    decimal Amount,
    RecurrenceFrequency Frequency,
    DateOnly NextDueDate);

public sealed record UpdateRecurringPaymentRequest(
    Guid AccountId,
    Guid? CategoryId,
    [Required(ErrorMessage = "Informe a descrição."), MaxLength(200)] string Description,
    decimal Amount,
    RecurrenceFrequency Frequency,
    DateOnly NextDueDate,
    bool IsActive);

public sealed record OccurrenceDto(
    Guid RecurringPaymentId,
    DateOnly DueDate,
    OccurrenceStatus Status,
    Guid? TransactionId,
    DateTime? PaidAt);

public sealed record GoalDto(
    Guid Id,
    string Name,
    decimal TargetAmount,
    decimal Accumulated,
    decimal ProgressPercent,
    DateOnly? Deadline,
    Guid? AccountId,
    GoalStatus Status,
    DateTime CreatedAt);

public sealed record CreateGoalRequest(
    [Required(ErrorMessage = "Informe o nome da meta."), MaxLength(120)] string Name,
    decimal TargetAmount,
    DateOnly? Deadline,
    Guid? AccountId);

public sealed record UpdateGoalRequest(
    [Required(ErrorMessage = "Informe o nome da meta."), MaxLength(120)] string Name,
    decimal TargetAmount,
    DateOnly? Deadline,
    Guid? AccountId);

public sealed record GoalMovementDto(
    Guid Id,
    Guid SavingGoalId,
    decimal Amount,
    GoalMovementType Type,
    DateOnly OccurredAt,
    DateTime CreatedAt);

public sealed record CreateGoalMovementRequest(
    decimal Amount,
    GoalMovementType Type,
    DateOnly OccurredAt);
