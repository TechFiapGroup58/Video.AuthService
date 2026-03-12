using AuthService.Application.DTOs;
using AuthService.Domain.Entities;

namespace AuthService.Application.Interfaces;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
}

public interface IUserRepository
{
    Task<ApplicationUser?> FindByEmailAsync(string email, CancellationToken ct = default);
    Task<bool> ExistsAsync(string email, CancellationToken ct = default);
    Task CreateAsync(ApplicationUser user, string password, CancellationToken ct = default);
    Task<bool> CheckPasswordAsync(ApplicationUser user, string password, CancellationToken ct = default);
}

public interface IJwtService
{
    (string Token, DateTime ExpiresAt) Generate(ApplicationUser user);
}
