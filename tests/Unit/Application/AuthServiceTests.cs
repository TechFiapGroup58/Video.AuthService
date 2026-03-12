using AuthService.Application.DTOs;
using AuthService.Application.Interfaces;
using AuthService.Application.Services;
using AuthService.Domain.Entities;
using AuthService.Domain.Exceptions;
using FluentAssertions;
using Moq;
using Xunit;

namespace AuthService.Tests.Unit.Application;

public sealed class AuthServiceTests
{
    private readonly Mock<IUserRepository> _repoMock = new();
    private readonly Mock<IJwtService>     _jwtMock  = new();
    private readonly AuthService.Application.Services.AuthService _sut;

    private static readonly (string Token, DateTime ExpiresAt) FakeJwt =
        ("fake.jwt.token", DateTime.UtcNow.AddHours(1));

    public AuthServiceTests()
    {
        _jwtMock.Setup(j => j.Generate(It.IsAny<ApplicationUser>())).Returns(FakeJwt);
        _sut = new AuthService.Application.Services.AuthService(_repoMock.Object, _jwtMock.Object);
    }

    // ── Register ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Register_NewUser_ReturnsAuthResponse()
    {
        // Arrange
        _repoMock.Setup(r => r.ExistsAsync("joao@email.com", default)).ReturnsAsync(false);
        _repoMock.Setup(r => r.CreateAsync(It.IsAny<ApplicationUser>(), "Senha@123", default))
                 .Returns(Task.CompletedTask);

        var request = new RegisterRequest("João Silva", "joao@email.com", "Senha@123");

        // Act
        var result = await _sut.RegisterAsync(request);

        // Assert
        result.Token.Should().Be(FakeJwt.Token);
        result.Email.Should().Be("joao@email.com");
        result.FullName.Should().Be("João Silva");
        _repoMock.Verify(r => r.CreateAsync(It.IsAny<ApplicationUser>(), "Senha@123", default), Times.Once);
    }

    [Fact]
    public async Task Register_ExistingEmail_ThrowsUserAlreadyExistsException()
    {
        // Arrange
        _repoMock.Setup(r => r.ExistsAsync("joao@email.com", default)).ReturnsAsync(true);

        var request = new RegisterRequest("João Silva", "joao@email.com", "Senha@123");

        // Act
        var act = () => _sut.RegisterAsync(request);

        // Assert
        await act.Should().ThrowAsync<UserAlreadyExistsException>()
                 .WithMessage("*joao@email.com*");
        _repoMock.Verify(r => r.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>(), default), Times.Never);
    }

    [Fact]
    public async Task Register_TrimsFullNameAndLowercasesEmail()
    {
        // Arrange
        _repoMock.Setup(r => r.ExistsAsync("joao@email.com", default)).ReturnsAsync(false);

        ApplicationUser? captured = null;
        _repoMock.Setup(r => r.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>(), default))
                 .Callback<ApplicationUser, string, CancellationToken>((u, _, _) => captured = u)
                 .Returns(Task.CompletedTask);

        var request = new RegisterRequest("  João  ", "JOAO@EMAIL.COM", "Senha@123");

        // Act
        await _sut.RegisterAsync(request);

        // Assert
        captured!.FullName.Should().Be("João");
        captured.Email.Should().Be("joao@email.com");
    }

    [Fact]
    public async Task Register_GeneratesJwtToken()
    {
        // Arrange
        _repoMock.Setup(r => r.ExistsAsync(It.IsAny<string>(), default)).ReturnsAsync(false);
        _repoMock.Setup(r => r.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>(), default))
                 .Returns(Task.CompletedTask);

        var request = new RegisterRequest("Ana", "ana@email.com", "Senha@123");

        // Act
        await _sut.RegisterAsync(request);

        // Assert
        _jwtMock.Verify(j => j.Generate(It.IsAny<ApplicationUser>()), Times.Once);
    }

    // ── Login ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Login_ValidCredentials_ReturnsAuthResponse()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id       = Guid.NewGuid(),
            FullName = "João Silva",
            Email    = "joao@email.com",
            UserName = "joao@email.com"
        };

        _repoMock.Setup(r => r.FindByEmailAsync("joao@email.com", default)).ReturnsAsync(user);
        _repoMock.Setup(r => r.CheckPasswordAsync(user, "Senha@123", default)).ReturnsAsync(true);

        var request = new LoginRequest("joao@email.com", "Senha@123");

        // Act
        var result = await _sut.LoginAsync(request);

        // Assert
        result.Token.Should().Be(FakeJwt.Token);
        result.Email.Should().Be("joao@email.com");
    }

    [Fact]
    public async Task Login_UserNotFound_ThrowsInvalidCredentialsException()
    {
        // Arrange
        _repoMock.Setup(r => r.FindByEmailAsync(It.IsAny<string>(), default))
                 .ReturnsAsync((ApplicationUser?)null);

        var request = new LoginRequest("naoexiste@email.com", "Senha@123");

        // Act
        var act = () => _sut.LoginAsync(request);

        // Assert
        await act.Should().ThrowAsync<InvalidCredentialsException>();
    }

    [Fact]
    public async Task Login_WrongPassword_ThrowsInvalidCredentialsException()
    {
        // Arrange
        var user = new ApplicationUser { Email = "joao@email.com" };
        _repoMock.Setup(r => r.FindByEmailAsync("joao@email.com", default)).ReturnsAsync(user);
        _repoMock.Setup(r => r.CheckPasswordAsync(user, "SenhaErrada", default)).ReturnsAsync(false);

        var request = new LoginRequest("joao@email.com", "SenhaErrada");

        // Act
        var act = () => _sut.LoginAsync(request);

        // Assert
        await act.Should().ThrowAsync<InvalidCredentialsException>();
    }

    [Fact]
    public async Task Login_LowercasesEmailBeforeSearch()
    {
        // Arrange
        var user = new ApplicationUser { Email = "joao@email.com", FullName = "João" };
        _repoMock.Setup(r => r.FindByEmailAsync("joao@email.com", default)).ReturnsAsync(user);
        _repoMock.Setup(r => r.CheckPasswordAsync(user, "Senha@123", default)).ReturnsAsync(true);

        var request = new LoginRequest("JOAO@EMAIL.COM", "Senha@123");

        // Act
        await _sut.LoginAsync(request);

        // Assert
        _repoMock.Verify(r => r.FindByEmailAsync("joao@email.com", default), Times.Once);
    }

    [Fact]
    public async Task Login_ValidCredentials_GeneratesJwt()
    {
        // Arrange
        var user = new ApplicationUser { Email = "joao@email.com", FullName = "João" };
        _repoMock.Setup(r => r.FindByEmailAsync("joao@email.com", default)).ReturnsAsync(user);
        _repoMock.Setup(r => r.CheckPasswordAsync(user, "Senha@123", default)).ReturnsAsync(true);

        // Act
        await _sut.LoginAsync(new LoginRequest("joao@email.com", "Senha@123"));

        // Assert
        _jwtMock.Verify(j => j.Generate(user), Times.Once);
    }
}
