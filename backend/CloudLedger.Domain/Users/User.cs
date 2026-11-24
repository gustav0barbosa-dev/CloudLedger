using CloudLedger.Domain.Common;

namespace CloudLedger.Domain.Users;

public sealed class User : Entity
{
    private User()
    {
    }

    public User(string name, string email, string passwordHash)
    {
        Name = Guard.NotEmpty(name, "nome", 120);
        Email = Guard.Email(email);
        PasswordHash = Guard.NotEmpty(passwordHash, "senha", 500);
        Status = UserStatus.Active;
    }

    public string Name { get; private set; } = string.Empty;

    public string Email { get; private set; } = string.Empty;

    public string PasswordHash { get; private set; } = string.Empty;

    public UserStatus Status { get; private set; } = UserStatus.Active;

    public void UpdateProfile(string name, string email)
    {
        Name = Guard.NotEmpty(name, "nome", 120);
        Email = Guard.Email(email);
    }

    public void ChangePassword(string passwordHash)
    {
        PasswordHash = Guard.NotEmpty(passwordHash, "senha", 500);
    }

    public void Disable() => Status = UserStatus.Disabled;

    public void Enable() => Status = UserStatus.Active;
}
