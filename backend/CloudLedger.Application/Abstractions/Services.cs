using CloudLedger.Domain.Audit;
using CloudLedger.Domain.Events;

namespace CloudLedger.Application.Abstractions;

public interface IClock
{
    DateTime UtcNow { get; }

    DateOnly Today { get; }
}

public interface ICurrentUser
{
    Guid? UserId { get; }

    bool IsAuthenticated { get; }

    Guid RequireUserId();
}

public interface ICorrelationContext
{
    string? CorrelationId { get; }
}

public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string hash);
}

public sealed record AccessToken(string Value, DateTime ExpiresAtUtc);

public interface ITokenService
{
    AccessToken CreateAccessToken(Domain.Users.User user);
}

public interface IRefreshTokenGenerator
{
    string Generate();

    string Hash(string token);
}

public interface IAuditWriter
{
    Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default);
}

public interface IAuditReader
{
    Task<Common.Models.PagedResult<AuditEvent>> ListAsync(
        Guid userId,
        DateTime? fromUtc,
        DateTime? toUtc,
        string? action,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}

public interface IDomainEventPublisher
{
    Task PublishAsync(DomainEvent domainEvent, CancellationToken cancellationToken = default);
}

public interface IIdempotencyStore
{
    /// <summary>
    /// Tenta reservar a chave. Retorna false quando a chave já foi processada
    /// com o mesmo hash (duplicata) e lança conflito quando o hash difere.
    /// </summary>
    Task<bool> TryBeginAsync(string key, string requestHash, TimeSpan ttl, CancellationToken cancellationToken = default);

    Task CompleteAsync(string key, string? resultReference, CancellationToken cancellationToken = default);

    Task FailAsync(string key, CancellationToken cancellationToken = default);
}

public interface IReportStorage
{
    Task<string> SaveAsync(string fileName, string contentType, string content, CancellationToken cancellationToken = default);
}

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default);
}
