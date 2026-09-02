using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MeuFinanceiro.Application.Auth;
using MeuFinanceiro.Application.Grupos;
using MeuFinanceiro.Domain.Enums;
using Xunit;

namespace MeuFinanceiro.Application.IntegrationTests;

/// <summary>
/// Cobre o AccessProfile (Completo/Editor/Visualizador) de um GrupoMember: bloqueio de
/// página (Configurações/Membros) para Editor/Visualizador, bloqueio de escrita para
/// Visualizador (WriteAccessFilter), e a proteção de que o Owner nunca pode ser rebaixado.
/// </summary>
[Trait("Category", "Integration")]
public class GrupoAccessProfileTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public GrupoAccessProfileTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static string UsernameFromEmail(string email) => $"user{email.Split('@')[0]}";

    private async Task<(HttpClient client, UserResponse user)> RegisterAsync(string name)
    {
        var id = Guid.NewGuid().ToString("N")[..12];
        var email = $"{id}@teste.com";
        return await AuthTestHelpers.RegisterConfirmAndLoginAsync(_factory, name, UsernameFromEmail(email), email, "SenhaForte!123");
    }

    private Task<(HttpClient client, UserResponse user)> LoginAgainAsync(string email) =>
        AuthTestHelpers.LoginAsync(_factory, UsernameFromEmail(email), "SenhaForte!123");

    private static Task<HttpResponseMessage> CreateAccountAsync(HttpClient client) =>
        client.PostAsJsonAsync("/api/v1/accounts", new { name = "Conta", bank = "Banco", type = 1, initialBalance = 0m });

    private async Task<(HttpClient clientA, UserResponse userA, string emailB)> SetupOwnerWithMemberAsync()
    {
        var (clientA, userA) = await RegisterAsync("Owner A");
        var (_, userB) = await RegisterAsync("Member B");

        var addResponse = await clientA.PostAsJsonAsync("/api/v1/grupos/me/members",
            new AddMemberRequest(userB.Email, GrupoRole.Member));
        addResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        return (clientA, userA, userB.Email);
    }

    [Fact]
    public async Task Rebaixar_membro_para_Editor_bloqueia_pagina_mas_nao_escrita()
    {
        var (clientA, _, emailB) = await SetupOwnerWithMemberAsync();

        // userB.Id precisa vir do grupo já movido — pega de novo via login.
        var (_, userBBefore) = await LoginAgainAsync(emailB);
        var updateResponse = await clientA.PutAsJsonAsync(
            $"/api/v1/grupos/me/members/{userBBefore.Id}/access-profile",
            new UpdateMemberAccessProfileRequest(GrupoAccessProfile.Editor));
        updateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var (clientB, _) = await LoginAgainAsync(emailB);

        (await clientB.GetAsync("/api/v1/grupos/me")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await CreateAccountAsync(clientB)).StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Rebaixar_membro_para_Visualizador_bloqueia_pagina_e_escrita()
    {
        var (clientA, _, emailB) = await SetupOwnerWithMemberAsync();
        var (_, userBBefore) = await LoginAgainAsync(emailB);

        var updateResponse = await clientA.PutAsJsonAsync(
            $"/api/v1/grupos/me/members/{userBBefore.Id}/access-profile",
            new UpdateMemberAccessProfileRequest(GrupoAccessProfile.Visualizador));
        updateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var (clientB, _) = await LoginAgainAsync(emailB);

        (await clientB.GetAsync("/api/v1/grupos/me")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await CreateAccountAsync(clientB)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await clientB.GetAsync("/api/v1/accounts?page=1&pageSize=50")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Owner_nunca_pode_ser_rebaixado()
    {
        var (clientA, userA) = await RegisterAsync("Owner A");

        var response = await clientA.PutAsJsonAsync(
            $"/api/v1/grupos/me/members/{userA.Id}/access-profile",
            new UpdateMemberAccessProfileRequest(GrupoAccessProfile.Visualizador));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Usuario_Visualizador_ainda_consegue_fazer_logout()
    {
        var (clientA, _, emailB) = await SetupOwnerWithMemberAsync();
        var (_, userBBefore) = await LoginAgainAsync(emailB);

        await clientA.PutAsJsonAsync(
            $"/api/v1/grupos/me/members/{userBBefore.Id}/access-profile",
            new UpdateMemberAccessProfileRequest(GrupoAccessProfile.Visualizador));

        var (clientB, _) = await LoginAgainAsync(emailB);

        // Logout não recebe corpo — o refresh token vai no cookie httpOnly, lido no backend.
        var logoutResponse = await clientB.PostAsync("/api/v1/auth/logout", null);
        logoutResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
