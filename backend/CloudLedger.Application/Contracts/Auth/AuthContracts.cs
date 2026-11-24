using System.ComponentModel.DataAnnotations;

namespace CloudLedger.Application.Contracts.Auth;

public sealed record RegisterRequest(
    [Required(ErrorMessage = "Informe o nome."), MaxLength(120)] string Name,
    [Required(ErrorMessage = "Informe o e-mail."), EmailAddress(ErrorMessage = "E-mail inválido."), MaxLength(256)] string Email,
    [Required(ErrorMessage = "Informe a senha."), MinLength(8, ErrorMessage = "A senha deve ter no mínimo 8 caracteres.")] string Password);

public sealed record LoginRequest(
    [Required(ErrorMessage = "Informe o e-mail."), EmailAddress(ErrorMessage = "E-mail inválido.")] string Email,
    [Required(ErrorMessage = "Informe a senha.")] string Password);

public sealed record RefreshRequest([Required] string RefreshToken);

public sealed record LogoutRequest([Required] string RefreshToken);

public sealed record UserDto(Guid Id, string Name, string Email, DateTime CreatedAt);

public sealed record AuthResponse(string AccessToken, int ExpiresIn, string RefreshToken, UserDto User);

public sealed record UpdateProfileRequest(
    [Required(ErrorMessage = "Informe o nome."), MaxLength(120)] string Name,
    [Required(ErrorMessage = "Informe o e-mail."), EmailAddress(ErrorMessage = "E-mail inválido."), MaxLength(256)] string Email);

public sealed record ChangePasswordRequest(
    [Required(ErrorMessage = "Informe a senha atual.")] string CurrentPassword,
    [Required(ErrorMessage = "Informe a nova senha."), MinLength(8, ErrorMessage = "A nova senha deve ter no mínimo 8 caracteres.")] string NewPassword);
