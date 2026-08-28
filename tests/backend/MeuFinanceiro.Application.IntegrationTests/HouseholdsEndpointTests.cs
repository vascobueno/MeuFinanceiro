using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using MeuFinanceiro.Application.Auth;
using MeuFinanceiro.Application.Households;
using MeuFinanceiro.Domain.Enums;
using Xunit;

namespace MeuFinanceiro.Application.IntegrationTests;

/// <summary>Cobre o fluxo de Household: provisionamento no registro, convite, remoção e isolamento entre households.</summary>
[Trait("Category", "Integration")]
public class HouseholdsEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public HouseholdsEndpointTests(CustomWebApplicationFactory factory)
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

    private static Task<HttpResponseMessage> CreateAccountAsync(HttpClient client) =>
        client.PostAsJsonAsync("/api/v1/accounts", new { name = "Conta", bank = "Banco", type = 1, initialBalance = 0m });

    [Fact]
    public async Task Registro_deve_criar_household_isolado_com_owner()
    {
        var (_, auth) = await RegisterAsync("Usuário A");

        auth.User.HouseholdId.Should().NotBe(Guid.Empty);
        auth.User.HouseholdRole.Should().Be(HouseholdRole.Owner);
    }

    [Fact]
    public async Task Household_de_B_nao_deve_ver_conta_criada_por_A()
    {
        var (clientA, _) = await RegisterAsync("Usuário A");
        var (clientB, _) = await RegisterAsync("Usuário B");

        (await CreateAccountAsync(clientA)).StatusCode.Should().Be(HttpStatusCode.Created);

        var accountsForB = await clientB.GetFromJsonAsync<PagedAccounts>("/api/v1/accounts?page=1&pageSize=50");
        accountsForB!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Owner_deve_conseguir_adicionar_membro_com_household_vazio_e_passar_a_compartilhar_dados()
    {
        var (clientA, authA) = await RegisterAsync("Usuário A");
        var (clientB, authB) = await RegisterAsync("Usuário B");

        (await CreateAccountAsync(clientA)).StatusCode.Should().Be(HttpStatusCode.Created);

        var addResponse = await clientA.PostAsJsonAsync("/api/v1/households/me/members",
            new AddMemberRequest(authB.User.Email, HouseholdRole.Member));
        addResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // B precisa de um novo token pra refletir o household novo (claims são fixadas na emissão).
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

        var addResponse = await clientA.PostAsJsonAsync("/api/v1/households/me/members",
            new AddMemberRequest(authC.User.Email, HouseholdRole.Member));

        addResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Member_nao_deve_conseguir_adicionar_outro_membro()
    {
        var (clientA, _) = await RegisterAsync("Usuário A");
        var (_, authB) = await RegisterAsync("Usuário B");
        var (_, authD) = await RegisterAsync("Usuário D");

        await clientA.PostAsJsonAsync("/api/v1/households/me/members", new AddMemberRequest(authB.User.Email, HouseholdRole.Member));
        var (clientBAfter, _) = await LoginAgainAsync(authB.User.Email);

        var forbidden = await clientBAfter.PostAsJsonAsync("/api/v1/households/me/members",
            new AddMemberRequest(authD.User.Email, HouseholdRole.Member));

        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Ao_remover_membro_ele_deve_ganhar_um_household_novo_e_vazio()
    {
        var (clientA, _) = await RegisterAsync("Usuário A");
        var (_, authB) = await RegisterAsync("Usuário B");

        await clientA.PostAsJsonAsync("/api/v1/households/me/members", new AddMemberRequest(authB.User.Email, HouseholdRole.Member));

        var removeResponse = await clientA.DeleteAsync($"/api/v1/households/me/members/{authB.User.Id}");
        removeResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var (clientBAfter, authBAfter) = await LoginAgainAsync(authB.User.Email);
        authBAfter.User.HouseholdId.Should().NotBe(authB.User.HouseholdId);
        authBAfter.User.HouseholdRole.Should().Be(HouseholdRole.Owner);

        var accountsForB = await clientBAfter.GetFromJsonAsync<PagedAccounts>("/api/v1/accounts?page=1&pageSize=50");
        accountsForB!.Items.Should().BeEmpty();
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

    private record PagedAccounts(List<object> Items);
}
