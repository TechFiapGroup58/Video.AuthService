using AuthService.Application.DTOs;
using AuthService.Application.Interfaces;
using AuthService.Domain.Entities;
using AuthService.Domain.Exceptions;

namespace AuthService.Application.Services;

public sealed class AuthService : IAuthService
{
    private readonly IUserRepository _users;
    private readonly IJwtService     _jwt;

    public AuthService(IUserRepository users, IJwtService jwt)
    {
        _users = users;
        _jwt   = jwt;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        if (await _users.ExistsAsync(request.Email, ct))
            throw new UserAlreadyExistsException(request.Email);

        var user = new ApplicationUser
        {
            Id       = Guid.NewGuid(),
            FullName = request.FullName.Trim(),
            Email    = request.Email.ToLowerInvariant().Trim(),
            UserName = request.Email.ToLowerInvariant().Trim()
        };

        await _users.CreateAsync(user, request.Password, ct);

        var (token, expiresAt) = _jwt.Generate(user);
        return new AuthResponse(token, user.Email!, user.FullName, expiresAt);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await _users.FindByEmailAsync(request.Email.ToLowerInvariant(), ct)
            ?? throw new InvalidCredentialsException();

        if (!await _users.CheckPasswordAsync(user, request.Password, ct))
            throw new InvalidCredentialsException();

        var (token, expiresAt) = _jwt.Generate(user);
        return new AuthResponse(token, user.Email!, user.FullName, expiresAt);
    }
}
