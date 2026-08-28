using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using MeuFinanceiro.Application.Auth;
using MeuFinanceiro.Application.Households;
using MeuFinanceiro.Domain.Enums;
using Xunit;

namespace MeuFinanceiro.Application.IntegrationTests;

/// <summary>
/// Cobre o AccessProfile (Completo/Editor/Visualizador) de um HouseholdMember: bloqueio de
/// página (Configurações/Membros) para Editor/Visualizador, bloqueio de escrita para
/// Visualizador (WriteAccessFilter), e a proteção de que o Owner nunca pode ser rebaixado.
/// </summary>
[Trait("Category", "Integration")]
public class HouseholdAccessProfileTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public HouseholdAccessProfileTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<(HttpClient client, AuthResponse auth)> RegisterAsync(string name)
    {
        var client = _factory.CreateClient();
        var email = $"{Guid.NewGuid():N}@teste.com";
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(name, email, "SenhaForte!123"));
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return (client, auth);
    }

    private async Task<(HttpClient client, AuthResponse auth)> LoginAgainAsync(string email)
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, "SenhaForte!123"));
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return (client, auth);
    }

    private static Task<HttpResponseMessage> CreateAccountAsync(HttpClient client) =>
        client.PostAsJsonAsync("/api/v1/accounts", new { name = "Conta", bank = "Banco", type = 1, initialBalance = 0m });

    private async Task<(HttpClient clientA, AuthResponse authA, string emailB)> SetupOwnerWithMemberAsync()
    {
        var (clientA, authA) = await RegisterAsync("Owner A");
        var (_, authB) = await RegisterAsync("Member B");

        var addResponse = await clientA.PostAsJsonAsync("/api/v1/households/me/members",
            new AddMemberRequest(authB.User.Email, HouseholdRole.Member));
        addResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        return (clientA, authA, authB.User.Email);
    }

    [Fact]
    public async Task Rebaixar_membro_para_Editor_bloqueia_pagina_mas_nao_escrita()
    {
        var (clientA, _, emailB) = await SetupOwnerWithMemberAsync();

        // authB.User.Id precisa vir do household já movido — pega de novo via login.
        var (_, authBBefore) = await LoginAgainAsync(emailB);
        var updateResponse = await clientA.PutAsJsonAsync(
            $"/api/v1/households/me/members/{authBBefore.User.Id}/access-profile",
            new UpdateMemberAccessProfileRequest(HouseholdAccessProfile.Editor));
        updateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var (clientB, _) = await LoginAgainAsync(emailB);

        (await clientB.GetAsync("/api/v1/households/me")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await CreateAccountAsync(clientB)).StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Rebaixar_membro_para_Visualizador_bloqueia_pagina_e_escrita()
    {
        var (clientA, _, emailB) = await SetupOwnerWithMemberAsync();
        var (_, authBBefore) = await LoginAgainAsync(emailB);

        var updateResponse = await clientA.PutAsJsonAsync(
            $"/api/v1/households/me/members/{authBBefore.User.Id}/access-profile",
            new UpdateMemberAccessProfileRequest(HouseholdAccessProfile.Visualizador));
        updateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var (clientB, _) = await LoginAgainAsync(emailB);

        (await clientB.GetAsync("/api/v1/households/me")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await CreateAccountAsync(clientB)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await clientB.GetAsync("/api/v1/accounts?page=1&pageSize=50")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Owner_nunca_pode_ser_rebaixado()
    {
        var (clientA, authA) = await RegisterAsync("Owner A");

        var response = await clientA.PutAsJsonAsync(
            $"/api/v1/households/me/members/{authA.User.Id}/access-profile",
            new UpdateMemberAccessProfileRequest(HouseholdAccessProfile.Visualizador));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Usuario_Visualizador_ainda_consegue_fazer_logout()
    {
        var (clientA, _, emailB) = await SetupOwnerWithMemberAsync();
        var (_, authBBefore) = await LoginAgainAsync(emailB);

        await clientA.PutAsJsonAsync(
            $"/api/v1/households/me/members/{authBBefore.User.Id}/access-profile",
            new UpdateMemberAccessProfileRequest(HouseholdAccessProfile.Visualizador));

        var (clientB, authB) = await LoginAgainAsync(emailB);

        var logoutResponse = await clientB.PostAsJsonAsync("/api/v1/auth/logout", new RefreshTokenRequest(authB.RefreshToken));
        logoutResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
