using AuthService.Application.Interfaces;
using AuthService.Domain.Entities;
using AuthService.Domain.Exceptions;
using Microsoft.AspNetCore.Identity;

namespace AuthService.Infrastructure.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly UserManager<ApplicationUser> _userManager;

    public UserRepository(UserManager<ApplicationUser> userManager)
        => _userManager = userManager;

    public async Task<ApplicationUser?> FindByEmailAsync(string email, CancellationToken ct = default)
        => await _userManager.FindByEmailAsync(email);

    public async Task<bool> ExistsAsync(string email, CancellationToken ct = default)
        => await _userManager.FindByEmailAsync(email) is not null;

    public async Task CreateAsync(ApplicationUser user, string password, CancellationToken ct = default)
    {
        var result = await _userManager.CreateAsync(user, password);
        if (!result.Succeeded)
            throw new DomainException(string.Join("; ", result.Errors.Select(e => e.Description)));
    }

    public async Task<bool> CheckPasswordAsync(ApplicationUser user, string password, CancellationToken ct = default)
        => await _userManager.CheckPasswordAsync(user, password);
}
