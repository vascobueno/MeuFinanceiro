using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MeuFinanceiro.Application.Auth;
using Xunit;

namespace MeuFinanceiro.Application.IntegrationTests;

/// <summary>Cobre o fluxo HTTP -> API -> Application -> Infrastructure -> EF Core (docs/20-testes.md).</summary>
[Trait("Category", "Integration")]
public class AccountsEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public AccountsEndpointTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _factory = factory;
    }

    [Fact]
    public async Task Deve_exigir_autenticacao_para_listar_contas()
    {
        var response = await _client.GetAsync("/api/v1/accounts");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Deve_registrar_logar_e_criar_uma_conta()
    {
        var id = Guid.NewGuid().ToString("N")[..12];
        var email = $"user-{id}@teste.com";
        var username = $"user{id}";

        var registerResponse = await _client.PostAsJsonAsync("/api/v1/auth/register",
            new RegisterRequest("Usuário Teste", username, email, "SenhaForte!123"));
        registerResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        await AuthTestHelpers.ConfirmEmailAsync(_client, _factory.EmailSender, email);

        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(username, "SenhaForte!123"));
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var user = await loginResponse.Content.ReadFromJsonAsync<UserResponse>();
        user.Should().NotBeNull();

        await AuthTestHelpers.AttachCsrfTokenAsync(_client);

        var createResponse = await _client.PostAsJsonAsync("/api/v1/accounts", new
        {
            name = "Conta Corrente Teste",
            bank = "Banco Teste",
            type = 1, // Checking
            initialBalance = 100.00m
        });

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var listResponse = await _client.GetAsync("/api/v1/accounts");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
