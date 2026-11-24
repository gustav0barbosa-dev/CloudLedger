using CloudLedger.Application.Abstractions;
using CloudLedger.Application.Abstractions.Persistence;
using CloudLedger.Application.Common;
using CloudLedger.Application.Common.Exceptions;
using CloudLedger.Application.Common.Security;
using CloudLedger.Application.Contracts.Auth;
using CloudLedger.Domain.Audit;
using CloudLedger.Domain.Common;
using CloudLedger.Domain.Users;

namespace CloudLedger.Application.Services;

public sealed class UserService
{
    private readonly IUserRepository _users;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditWriter _audit;
    private readonly ICorrelationContext _correlation;
    private readonly IClock _clock;

    public UserService(
        IUserRepository users,
        IRefreshTokenRepository refreshTokens,
        IPasswordHasher passwordHasher,
        IUnitOfWork unitOfWork,
        IAuditWriter audit,
        ICorrelationContext correlation,
        IClock clock)
    {
        _users = users;
        _refreshTokens = refreshTokens;
        _passwordHasher = passwordHasher;
        _unitOfWork = unitOfWork;
        _audit = audit;
        _correlation = correlation;
        _clock = clock;
    }

    public async Task<UserDto> GetAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Usuário");

        return ToDto(user);
    }

    public async Task<UserDto> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Usuário");

        var email = Guard.Email(request.Email);
        if (!string.Equals(email, user.Email, StringComparison.OrdinalIgnoreCase)
            && await _users.EmailExistsAsync(email, cancellationToken))
        {
            throw new ConflictException("email.duplicate", "Já existe uma conta com este e-mail.");
        }

        user.UpdateProfile(request.Name, email);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _audit.WriteAsync(
            AuditEventFactory.Create(user.Id, AuditActions.UserProfileUpdated, nameof(User), user.Id, _correlation.CorrelationId, _clock.UtcNow),
            cancellationToken);

        return ToDto(user);
    }

    public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Usuário");

        if (!_passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
        {
            throw new ValidationAppException("currentPassword", "A senha atual está incorreta.");
        }

        PasswordPolicy.EnsureValid(request.NewPassword);
        user.ChangePassword(_passwordHasher.Hash(request.NewPassword));
        await _refreshTokens.RevokeAllAsync(user.Id, _clock.UtcNow, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _audit.WriteAsync(
            AuditEventFactory.Create(user.Id, AuditActions.UserPasswordChanged, nameof(User), user.Id, _correlation.CorrelationId, _clock.UtcNow),
            cancellationToken);
    }

    private static UserDto ToDto(User user) => new(user.Id, user.Name, user.Email, user.CreatedAt);
}
