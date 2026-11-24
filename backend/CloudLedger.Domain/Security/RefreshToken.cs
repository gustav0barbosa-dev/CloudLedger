using CloudLedger.Domain.Common;

namespace CloudLedger.Domain.Security;

/// <summary>
/// Refresh token opaco: apenas o hash SHA-256 é persistido. A rotação
/// invalida o token anterior e registra o substituto.
/// </summary>
public sealed class RefreshToken : Entity
{
    private RefreshToken()
    {
    }

    public RefreshToken(Guid userId, string tokenHash, DateTime expiresAtUtc)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("refresh_token.user_required", "O token precisa estar vinculado a um usuário.");
        }

        UserId = userId;
        TokenHash = Guard.NotEmpty(tokenHash, "token", 128);
        ExpiresAt = expiresAtUtc;
    }

    public Guid UserId { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public DateTime ExpiresAt { get; private set; }

    public DateTime? RevokedAt { get; private set; }

    public string? ReplacedByTokenHash { get; private set; }

    public bool IsActive(DateTime utcNow) => RevokedAt is null && ExpiresAt > utcNow;

    public void Revoke(DateTime utcNow, string? replacedByTokenHash = null)
    {
        if (RevokedAt is not null)
        {
            return;
        }

        RevokedAt = utcNow;
        ReplacedByTokenHash = replacedByTokenHash;
    }
}
