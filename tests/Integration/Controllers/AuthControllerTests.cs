using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AuthService.Application.DTOs;
using AuthService.Domain.Entities;
using AuthService.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AuthService.Tests.Integration.Controllers;

// ── Factory ──────────────────────────────────────────────────────────────────

public sealed class AuthWebAppFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Troca Postgres por InMemory
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor is not null) services.Remove(descriptor);

            services.AddDbContext<AppDbContext>(opt =>
                opt.UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}"));

            // Cria schema
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
        });
    }
}

// ── Tests ─────────────────────────────────────────────────────────────────────

public sealed class AuthControllerTests : IClassFixture<AuthWebAppFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNameCaseInsensitive = true };

    public AuthControllerTests(AuthWebAppFactory factory)
        => _client = factory.CreateClient();

    // helpers
    private static RegisterRequest NewRegisterRequest(string suffix = "") => new(
        FullName: "João Silva",
        Email:    $"joao{suffix}@test.com",
        Password: "Senha@123"
    );

    // ── POST /api/auth/register ───────────────────────────────────────────────

    [Fact]
    public async Task Register_ValidRequest_Returns201WithToken()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", NewRegisterRequest("1"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = JsonSerializer.Deserialize<AuthResponse>(
            await response.Content.ReadAsStringAsync(), JsonOpts);

        body!.Token.Should().NotBeNullOrWhiteSpace();
        body.Email.Should().Be("joao1@test.com");
        body.FullName.Should().Be("João Silva");
        body.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns409()
    {
        await _client.PostAsJsonAsync("/api/auth/register", NewRegisterRequest("2"));

        var response = await _client.PostAsJsonAsync("/api/auth/register", NewRegisterRequest("2"));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Register_InvalidEmail_Returns400()
    {
        var request = new RegisterRequest("João", "email-invalido", "Senha@123");

        var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_ShortPassword_Returns400()
    {
        var request = new RegisterRequest("João", "joao@test.com", "123");

        var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_EmptyBody_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new { });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── POST /api/auth/login ─────────────────────────────────────────────────

    [Fact]
    public async Task Login_ValidCredentials_Returns200WithToken()
    {
        await _client.PostAsJsonAsync("/api/auth/register", NewRegisterRequest("3"));

        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("joao3@test.com", "Senha@123"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = JsonSerializer.Deserialize<AuthResponse>(
            await response.Content.ReadAsStringAsync(), JsonOpts);

        body!.Token.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        await _client.PostAsJsonAsync("/api/auth/register", NewRegisterRequest("4"));

        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("joao4@test.com", "SenhaErrada"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_UnknownEmail_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("naoexiste@test.com", "Senha@123"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_EmptyBody_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new { });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── Health ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task HealthCheck_Returns200()
    {
        var response = await _client.GetAsync("/health");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
