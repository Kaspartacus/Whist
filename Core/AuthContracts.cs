using System.ComponentModel.DataAnnotations;

namespace Core;

public sealed class LoginRequest
{
    [Required, EmailAddress, MaxLength(100)]
    public string Email { get; set; } = "";

    [Required, MaxLength(200)]
    public string Password { get; set; } = "";
}

public sealed class LoginResponse
{
    public string AccessToken { get; set; } = "";
    public DateTime ExpiresAtUtc { get; set; }
    public string RefreshToken { get; set; } = "";
    public DateTime RefreshTokenExpiresAtUtc { get; set; }
    public AuthenticatedUser User { get; set; } = new();
}

public sealed class RefreshTokenRequest
{
    [Required, MaxLength(500)]
    public string RefreshToken { get; set; } = "";
}

public sealed class LogoutRequest
{
    [MaxLength(500)]
    public string? RefreshToken { get; set; }
}

public sealed class AuthenticatedUser
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string[] Roles { get; set; } = Array.Empty<string>();
}

public sealed class SaveUserRequest
{
    public int Id { get; set; }

    [Required, MaxLength(40)]
    public string Name { get; set; } = "";

    [Required, MaxLength(30)]
    public string NickName { get; set; } = "";

    [Required, EmailAddress, MaxLength(100)]
    public string Email { get; set; } = "";

    [MaxLength(200)]
    public string? Password { get; set; }

    [Required, MaxLength(200)]
    public string Address { get; set; } = "";

    [Required, RegularExpression(@"^\d{8}$", ErrorMessage = "Telefonnummer skal være 8 cifre.")]
    public string PhoneNumber { get; set; } = "";

    public DateOnly? BirthDate { get; set; }

    [MaxLength(500)]
    public string Description { get; set; } = "";

    [MaxLength(200)]
    public string FunFact { get; set; } = "";

    [MaxLength(500)]
    public string ImageUrl { get; set; } = "";
}

public sealed class ChangePasswordRequest
{
    [Required, MaxLength(200)]
    public string CurrentPassword { get; set; } = "";

    [Required, MinLength(8), MaxLength(200)]
    public string NewPassword { get; set; } = "";
}

public sealed class ResetPasswordRequest
{
    [Required, MinLength(8), MaxLength(200)]
    public string NewPassword { get; set; } = "";
}
