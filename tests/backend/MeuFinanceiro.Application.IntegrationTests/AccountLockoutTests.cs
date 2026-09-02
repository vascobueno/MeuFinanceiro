using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MeuFinanceiro.Application.Auth;
using MeuFinanceiro.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MeuFinanceiro.Application.IntegrationTests;

/// <summary>
/// Cobre o bloqueio de conta por tentativas de senha errada (Lockout.MaxFailedAccessAttempts
/// = 5), configurado em DependencyInjection.cs e aplicado via
/// SignInManager.CheckPasswordSignInAsync em AuthService.LoginAsync.
/// </summary>
[Trait("Category", "Integration")]
public class AccountLockoutTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AccountLockoutTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private const string Password = "SenhaForte!123";

    private async Task<string> RegisterAsync(string name)
    {
        var client = _factory.CreateClient();
        var id = Guid.NewGuid().ToString("N")[..12];
        var username = $"user{id}";
        var email = $"{id}@teste.com";
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(name, username, email, Password));
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await AuthTestHelpers.ConfirmEmailAsync(client, _factory.EmailSender, email);

        return username;
    }

    [Fact]
    public async Task Cinco_tentativas_erradas_bloqueiam_a_conta_mesmo_com_a_senha_certa_depois()
    {
        var username = await RegisterAsync("Usuário Lockout");

        for (var i = 0; i < 5; i++)
        {
            var client = _factory.CreateClient();
            var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(username, "SenhaErrada!999"));
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        // 6ª tentativa, agora com a senha CERTA — ainda deve falhar, a conta está bloqueada.
        var lockedClient = _factory.CreateClient();
        var lockedResponse = await lockedClient.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(username, Password));
        lockedResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByNameAsync(username);
        (await userManager.IsLockedOutAsync(user!)).Should().BeTrue();
    }

    [Fact]
    public async Task Login_correto_antes_do_limite_nao_bloqueia_a_conta()
    {
        var username = await RegisterAsync("Usuário Sem Lockout");

        for (var i = 0; i < 3; i++)
        {
            var client = _factory.CreateClient();
            await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(username, "SenhaErrada!999"));
        }

        var okClient = _factory.CreateClient();
        var response = await okClient.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(username, Password));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
