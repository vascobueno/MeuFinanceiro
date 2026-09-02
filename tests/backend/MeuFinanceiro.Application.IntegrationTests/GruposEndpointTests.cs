using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using MeuFinanceiro.Application.Auth;
using MeuFinanceiro.Application.Grupos;
using MeuFinanceiro.Domain.Enums;
using Xunit;

namespace MeuFinanceiro.Application.IntegrationTests;

/// <summary>Cobre o fluxo de Grupo: provisionamento no registro, convite, remoção e isolamento entre grupos.</summary>
[Trait("Category", "Integration")]
public class GruposEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public GruposEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static string UsernameFromEmail(string email) => $"user{email.Split('@')[0]}";

    private async Task<(HttpClient client, AuthResponse auth)> RegisterAsync(string name)
    {
        var client = _factory.CreateClient();
        var id = Guid.NewGuid().ToString("N")[..12];
        var email = $"{id}@teste.com";
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(name, UsernameFromEmail(email), email, "SenhaForte!123"));
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return (client, auth);
    }

    private static Task<HttpResponseMessage> CreateAccountAsync(HttpClient client) =>
        client.PostAsJsonAsync("/api/v1/accounts", new { name = "Conta", bank = "Banco", type = 1, initialBalance = 0m });

    [Fact]
    public async Task Registro_deve_criar_grupo_isolado_com_owner()
    {
        var (_, auth) = await RegisterAsync("Usuário A");

        auth.User.GrupoId.Should().NotBe(Guid.Empty);
        auth.User.GrupoRole.Should().Be(GrupoRole.Owner);
    }

    [Fact]
    public async Task Grupo_de_B_nao_deve_ver_conta_criada_por_A()
    {
        var (clientA, _) = await RegisterAsync("Usuário A");
        var (clientB, _) = await RegisterAsync("Usuário B");

        (await CreateAccountAsync(clientA)).StatusCode.Should().Be(HttpStatusCode.Created);

        var accountsForB = await clientB.GetFromJsonAsync<PagedAccounts>("/api/v1/accounts?page=1&pageSize=50");
        accountsForB!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Owner_deve_conseguir_adicionar_membro_com_grupo_vazio_e_passar_a_compartilhar_dados()
    {
        var (clientA, authA) = await RegisterAsync("Usuário A");
        var (clientB, authB) = await RegisterAsync("Usuário B");

        (await CreateAccountAsync(clientA)).StatusCode.Should().Be(HttpStatusCode.Created);

        var addResponse = await clientA.PostAsJsonAsync("/api/v1/grupos/me/members",
            new AddMemberRequest(authB.User.Email, GrupoRole.Member));
        addResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // B precisa de um novo token pra refletir o grupo novo (claims são fixadas na emissão).
        var (clientBAfter, _) = await LoginAgainAsync(authB.User.Email);
        var accountsForB = await clientBAfter.GetFromJsonAsync<PagedAccounts>("/api/v1/accounts?page=1&pageSize=50");
        accountsForB!.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task Convite_deve_ser_recusado_se_convidado_ja_tiver_dados_proprios()
    {
        var (clientA, _) = await RegisterAsync("Usuário A");
        var (clientC, authC) = await RegisterAsync("Usuário C");

        (await CreateAccountAsync(clientC)).StatusCode.Should().Be(HttpStatusCode.Created);

        var addResponse = await clientA.PostAsJsonAsync("/api/v1/grupos/me/members",
            new AddMemberRequest(authC.User.Email, GrupoRole.Member));

        addResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Member_nao_deve_conseguir_adicionar_outro_membro()
    {
        var (clientA, _) = await RegisterAsync("Usuário A");
        var (_, authB) = await RegisterAsync("Usuário B");
        var (_, authD) = await RegisterAsync("Usuário D");

        await clientA.PostAsJsonAsync("/api/v1/grupos/me/members", new AddMemberRequest(authB.User.Email, GrupoRole.Member));
        var (clientBAfter, _) = await LoginAgainAsync(authB.User.Email);

        var forbidden = await clientBAfter.PostAsJsonAsync("/api/v1/grupos/me/members",
            new AddMemberRequest(authD.User.Email, GrupoRole.Member));

        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Ao_remover_membro_ele_deve_ganhar_um_grupo_novo_e_vazio()
    {
        var (clientA, _) = await RegisterAsync("Usuário A");
        var (_, authB) = await RegisterAsync("Usuário B");

        await clientA.PostAsJsonAsync("/api/v1/grupos/me/members", new AddMemberRequest(authB.User.Email, GrupoRole.Member));

        var removeResponse = await clientA.DeleteAsync($"/api/v1/grupos/me/members/{authB.User.Id}");
        removeResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var (clientBAfter, authBAfter) = await LoginAgainAsync(authB.User.Email);
        authBAfter.User.GrupoId.Should().NotBe(authB.User.GrupoId);
        authBAfter.User.GrupoRole.Should().Be(GrupoRole.Owner);

        var accountsForB = await clientBAfter.GetFromJsonAsync<PagedAccounts>("/api/v1/accounts?page=1&pageSize=50");
        accountsForB!.Items.Should().BeEmpty();
    }

    private async Task<(HttpClient client, AuthResponse auth)> LoginAgainAsync(string email)
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(UsernameFromEmail(email), "SenhaForte!123"));
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return (client, auth);
    }

    private record PagedAccounts(List<object> Items);
}
