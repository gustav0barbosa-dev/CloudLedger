using System.ComponentModel.DataAnnotations;
using CloudLedger.Domain.Accounts;
using CloudLedger.Domain.Categories;
using CloudLedger.Domain.Transactions;
using CloudLedger.Domain.Transfers;

namespace CloudLedger.Application.Contracts.Finance;

public sealed record AccountDto(
    Guid Id,
    string Name,
    AccountType Type,
    string Currency,
    decimal OpeningBalance,
    decimal Balance,
    bool IsArchived,
    DateTime CreatedAt);

public sealed record CreateAccountRequest(
    [Required(ErrorMessage = "Informe o nome da conta."), MaxLength(120)] string Name,
    AccountType Type,
    decimal OpeningBalance,
    [MaxLength(3)] string? Currency);

public sealed record UpdateAccountRequest(
    [Required(ErrorMessage = "Informe o nome da conta."), MaxLength(120)] string Name,
    AccountType Type,
    decimal OpeningBalance);

public sealed record CategoryDto(
    Guid Id,
    string Name,
    CategoryKind Kind,
    bool IsDefault,
    string? Color,
    string? Icon);

public sealed record CreateCategoryRequest(
    [Required(ErrorMessage = "Informe o nome da categoria."), MaxLength(80)] string Name,
    CategoryKind Kind,
    [MaxLength(7)] string? Color,
    [MaxLength(16)] string? Icon);

public sealed record UpdateCategoryRequest(
    [Required(ErrorMessage = "Informe o nome da categoria."), MaxLength(80)] string Name,
    [MaxLength(7)] string? Color,
    [MaxLength(16)] string? Icon);

public sealed record TransactionDto(
    Guid Id,
    Guid AccountId,
    string AccountName,
    Guid? CategoryId,
    string? CategoryName,
    TransactionType Type,
    decimal Amount,
    string Description,
    DateOnly OccurredAt,
    TransactionStatus Status,
    DateTime CreatedAt,
    DateTime? VoidedAt);

public sealed record CreateTransactionRequest(
    Guid AccountId,
    Guid? CategoryId,
    TransactionType Type,
    decimal Amount,
    [Required(ErrorMessage = "Informe a descrição."), MaxLength(200)] string Description,
    DateOnly OccurredAt);

public sealed record UpdateTransactionRequest(
    Guid AccountId,
    Guid? CategoryId,
    TransactionType Type,
    decimal Amount,
    [Required(ErrorMessage = "Informe a descrição."), MaxLength(200)] string Description,
    DateOnly OccurredAt);

public sealed record TransferDto(
    Guid Id,
    Guid SourceAccountId,
    string SourceAccountName,
    Guid DestinationAccountId,
    string DestinationAccountName,
    decimal Amount,
    DateOnly OccurredAt,
    string? Description,
    TransferStatus Status,
    DateTime CreatedAt,
    DateTime? VoidedAt);

public sealed record CreateTransferRequest(
    Guid SourceAccountId,
    Guid DestinationAccountId,
    decimal Amount,
    DateOnly OccurredAt,
    [MaxLength(200)] string? Description);
