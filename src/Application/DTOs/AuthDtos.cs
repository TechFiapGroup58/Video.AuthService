using System.ComponentModel.DataAnnotations;

namespace AuthService.Application.DTOs;

public sealed record RegisterRequest(
    [Required, MinLength(3), MaxLength(150)] string FullName,
    [Required, EmailAddress, MaxLength(256)]  string Email,
    [Required, MinLength(8)]                  string Password
);

public sealed record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required]               string Password
);

public sealed record AuthResponse(
    string Token,
    string Email,
    string FullName,
    DateTime ExpiresAt
);
