using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AuthService.Application.DTOs;
using AuthService.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AuthService.Tests.Integration.Controllers;

// ── Factory ──────────────────────────────────────────────────────────────────

public sealed class AuthWebAppFactory : WebApplicationFactory<Program>
{
    // Nome fixo: todos os testes desta factory compartilham o mesmo banco em memória
    private readonly string _dbName = $"TestDb_{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // Injeta configurações mínimas para JWT funcionar sem appsettings real
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"]      = "test-secret-key-at-least-32-chars-long!!",
                ["Jwt:Issuer"]         = "fiapx-auth",
                ["Jwt:Audience"]       = "fiapx-services",
                ["Jwt:ExpiresMinutes"] = "60",
            });
        });

        builder.ConfigureServices(services =>
        {
            // Remove o DbContext real (Npgsql)
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor is not null) services.Remove(descriptor);

            // Substitui por InMemory com nome FIXO para esta instância de factory
            services.AddDbContext<AppDbContext>(opt =>
                opt.UseInMemoryDatabase(_dbName));

            // Garante que o schema seja criado antes dos testes rodarem
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
        });
    }
}

// ── Tests ─────────────────────────────────────────────────────────────────────

public sealed class AuthControllerTests : IClassFixture<AuthWebAppFactory>
{
    private readonly AuthWebAppFactory _factory;
    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNameCaseInsensitive = true };

    public AuthControllerTests(AuthWebAppFactory factory)
        => _factory = factory;

    // Cada teste recebe um cliente próprio (sem cookies/state compartilhado)
    private HttpClient Client() => _factory.CreateClient();

    // Email único por chamada — evita colisão entre testes paralelos
    private static string UniqueEmail() => $"user_{Guid.NewGuid():N}@test.com";

    private static RegisterRequest NewRegisterRequest(string? email = null) => new(
        FullName: "João Silva",
        Email:    email ?? UniqueEmail(),
        Password: "Senha@123"
    );

    // ── POST /api/auth/register ───────────────────────────────────────────────

    [Fact]
    public async Task Register_ValidRequest_Returns201WithToken()
    {
        var email    = UniqueEmail();
        var response = await Client().PostAsJsonAsync("/api/auth/register",
            NewRegisterRequest(email));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = JsonSerializer.Deserialize<AuthResponse>(
            await response.Content.ReadAsStringAsync(), JsonOpts);

        body!.Token.Should().NotBeNullOrWhiteSpace();
        body.Email.Should().Be(email);
        body.FullName.Should().Be("João Silva");
        body.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns409()
    {
        var client = Client();
        var email   = UniqueEmail();

        await client.PostAsJsonAsync("/api/auth/register", NewRegisterRequest(email));
        var response = await client.PostAsJsonAsync("/api/auth/register", NewRegisterRequest(email));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Register_InvalidEmail_Returns400()
    {
        var response = await Client().PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("João", "email-invalido", "Senha@123"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_ShortPassword_Returns400()
    {
        var response = await Client().PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("João", UniqueEmail(), "123"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_EmptyBody_Returns400()
    {
        var response = await Client().PostAsJsonAsync("/api/auth/register", new { });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── POST /api/auth/login ─────────────────────────────────────────────────

    [Fact]
    public async Task Login_ValidCredentials_Returns200WithToken()
    {
        var client   = Client();
        var email    = UniqueEmail();
        var password = "Senha@123";

        // Registra primeiro
        var register = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("João Silva", email, password));
        register.StatusCode.Should().Be(HttpStatusCode.Created, "registro deve funcionar antes do login");

        // Login
        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(email, password));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = JsonSerializer.Deserialize<AuthResponse>(
            await response.Content.ReadAsStringAsync(), JsonOpts);

        body!.Token.Should().NotBeNullOrWhiteSpace();
        body.Email.Should().Be(email);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        var client = Client();
        var email  = UniqueEmail();

        await client.PostAsJsonAsync("/api/auth/register", NewRegisterRequest(email));

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(email, "SenhaErrada1"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_UnknownEmail_Returns401()
    {
        var response = await Client().PostAsJsonAsync("/api/auth/login",
            new LoginRequest(UniqueEmail(), "Senha@123"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_EmptyBody_Returns400()
    {
        var response = await Client().PostAsJsonAsync("/api/auth/login", new { });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── Health ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task HealthCheck_Returns200()
    {
        var response = await Client().GetAsync("/health");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
