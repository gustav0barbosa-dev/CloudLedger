using CloudLedger.Application.Abstractions;
using CloudLedger.Application.Abstractions.Persistence;
using CloudLedger.Application.Common;
using CloudLedger.Application.Common.Exceptions;
using CloudLedger.Application.Common.Options;
using CloudLedger.Application.Common.Security;
using CloudLedger.Application.Contracts.Auth;
using CloudLedger.Domain.Audit;
using CloudLedger.Domain.Common;
using CloudLedger.Domain.Security;
using CloudLedger.Domain.Users;
using Microsoft.Extensions.Options;

namespace CloudLedger.Application.Services;

public sealed class AuthService
{
    private readonly IUserRepository _users;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly IRefreshTokenGenerator _refreshTokenGenerator;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditWriter _audit;
    private readonly ICorrelationContext _correlation;
    private readonly IClock _clock;
    private readonly JwtOptions _jwt;

    public AuthService(
        IUserRepository users,
        IRefreshTokenRepository refreshTokens,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IRefreshTokenGenerator refreshTokenGenerator,
        IUnitOfWork unitOfWork,
        IAuditWriter audit,
        ICorrelationContext correlation,
        IClock clock,
        IOptions<JwtOptions> jwt)
    {
        _users = users;
        _refreshTokens = refreshTokens;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _refreshTokenGenerator = refreshTokenGenerator;
        _unitOfWork = unitOfWork;
        _audit = audit;
        _correlation = correlation;
        _clock = clock;
        _jwt = jwt.Value;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        PasswordPolicy.EnsureValid(request.Password);

        var email = Guard.Email(request.Email);
        if (await _users.EmailExistsAsync(email, cancellationToken))
        {
            throw new ConflictException("email.duplicate", "Já existe uma conta com este e-mail.");
        }

        var user = new User(request.Name, email, _passwordHasher.Hash(request.Password));
        _users.Add(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _audit.WriteAsync(
            AuditEventFactory.Create(user.Id, AuditActions.UserRegistered, nameof(User), user.Id, _correlation.CorrelationId, _clock.UtcNow),
            cancellationToken);

        return await IssueTokensAsync(user, cancellationToken);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var email = Guard.Email(request.Email);
        var user = await _users.GetByEmailAsync(email, cancellationToken);

        if (user is null || !_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            throw new UnauthorizedException("E-mail ou senha inválidos.");
        }

        if (user.Status != UserStatus.Active)
        {
            throw new ForbiddenException("Esta conta está desativada.");
        }

        await _audit.WriteAsync(
            AuditEventFactory.Create(user.Id, AuditActions.UserLoggedIn, nameof(User), user.Id, _correlation.CorrelationId, _clock.UtcNow),
            cancellationToken);

        return await IssueTokensAsync(user, cancellationToken);
    }

    public async Task<AuthResponse> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken = default)
    {
        var hash = _refreshTokenGenerator.Hash(request.RefreshToken);
        var existing = await _refreshTokens.GetByHashAsync(hash, cancellationToken);

        if (existing is null || !existing.IsActive(_clock.UtcNow))
        {
            throw new UnauthorizedException("Sessão expirada. Faça login novamente.");
        }

        var user = await _users.GetByIdAsync(existing.UserId, cancellationToken)
            ?? throw new UnauthorizedException("Sessão inválida.");

        if (user.Status != UserStatus.Active)
        {
            throw new ForbiddenException("Esta conta está desativada.");
        }

        var rawToken = _refreshTokenGenerator.Generate();
        var newHash = _refreshTokenGenerator.Hash(rawToken);
        existing.Revoke(_clock.UtcNow, newHash);

        _refreshTokens.Add(new RefreshToken(user.Id, newHash, _clock.UtcNow.AddDays(_jwt.RefreshTokenDays)));
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var accessToken = _tokenService.CreateAccessToken(user);
        return BuildResponse(user, accessToken, rawToken);
    }

    public async Task LogoutAsync(string refreshToken, Guid? userId, CancellationToken cancellationToken = default)
    {
        var hash = _refreshTokenGenerator.Hash(refreshToken);
        var existing = await _refreshTokens.GetByHashAsync(hash, cancellationToken);

        if (existing is null)
        {
            return;
        }

        if (userId is not null && existing.UserId != userId)
        {
            return;
        }

        existing.Revoke(_clock.UtcNow);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _audit.WriteAsync(
            AuditEventFactory.Create(existing.UserId, AuditActions.UserLoggedOut, nameof(User), existing.UserId, _correlation.CorrelationId, _clock.UtcNow),
            cancellationToken);
    }

    private async Task<AuthResponse> IssueTokensAsync(User user, CancellationToken cancellationToken)
    {
        var accessToken = _tokenService.CreateAccessToken(user);
        var rawToken = _refreshTokenGenerator.Generate();

        _refreshTokens.Add(new RefreshToken(user.Id, _refreshTokenGenerator.Hash(rawToken), _clock.UtcNow.AddDays(_jwt.RefreshTokenDays)));
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return BuildResponse(user, accessToken, rawToken);
    }

    private AuthResponse BuildResponse(User user, AccessToken accessToken, string refreshToken)
    {
        var expiresIn = (int)Math.Max(60, (accessToken.ExpiresAtUtc - _clock.UtcNow).TotalSeconds);
        return new AuthResponse(
            accessToken.Value,
            expiresIn,
            refreshToken,
            new UserDto(user.Id, user.Name, user.Email, user.CreatedAt));
    }
}
