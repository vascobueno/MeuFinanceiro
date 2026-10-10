using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using MeuFinanceiro.Domain.Enums;
using Xunit;

namespace MeuFinanceiro.Application.IntegrationTests;

/// <summary>Grupo de cartões criado junto com o titular, numa gravação só (docs/04).</summary>
[Trait("Category", "Integration")]
public class CardGroupWithTitularTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public CardGroupWithTitularTests(CustomWebApplicationFactory factory) => _factory = factory;

    private async Task<HttpClient> NovoUsuarioAsync()
    {
        var id = Guid.NewGuid().ToString("N")[..12];
        var (client, _) = await AuthTestHelpers.RegisterConfirmAndLoginAsync(
            _factory, "Usuário Grupo", $"grupo{id}", $"grupo-{id}@teste.com", "SenhaForte!123");
        return client;
    }

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.Should().BeTrue(body);
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static object Grupo(string nome, object? titular) =>
        new { name = nome, brand = "Visa", closingDay = 2, dueDay = 10, limit = 3000m, titular };

    private static async Task<int> QuantosGruposAsync(HttpClient client) =>
        (await JsonAsync(await client.GetAsync("/api/v1/cards?pageSize=100"))).GetProperty("totalItems").GetInt32();

    [Fact]
    public async Task Cria_o_grupo_e_o_titular_juntos()
    {
        var client = await NovoUsuarioAsync();
        var conta = (await JsonAsync(await client.PostAsJsonAsync("/api/v1/accounts",
            new { name = "Conta Ficticia", type = 1, initialBalance = 0m }))).GetProperty("id").GetGuid();

        // Mandar "Adicional" não muda nada: o cartão criado com o grupo é o do titular.
        var grupo = await JsonAsync(await client.PostAsJsonAsync("/api/v1/cards", Grupo("Grupo Ficticio",
            new { kind = 2, lastFour = "1234", firstDigits = "4532", holderName = "PORTADOR FICTICIO", accountId = conta })));
        var id = grupo.GetProperty("id").GetGuid();

        var cartoes = (await JsonAsync(await client.GetAsync($"/api/v1/cards/{id}/issued-cards"))).EnumerateArray().ToList();
        cartoes.Should().ContainSingle();
        cartoes[0].GetProperty("kind").GetInt32().Should().Be((int)IssuedCardKind.Titular);
        cartoes[0].GetProperty("lastFour").GetString().Should().Be("1234");
        cartoes[0].GetProperty("accountId").GetGuid().Should().Be(conta);
    }

    [Fact]
    public async Task Sem_titular_continua_criando_so_o_grupo()
    {
        var client = await NovoUsuarioAsync();
        var id = (await JsonAsync(await client.PostAsJsonAsync("/api/v1/cards", Grupo("Só grupo", null)))).GetProperty("id").GetGuid();
        (await JsonAsync(await client.GetAsync($"/api/v1/cards/{id}/issued-cards"))).GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Titular_recusado_nao_deixa_grupo_vazio_para_tras()
    {
        var client = await NovoUsuarioAsync();
        await JsonAsync(await client.PostAsJsonAsync("/api/v1/cards", Grupo("Primeiro", new { kind = 1, lastFour = "5678" })));
        var antes = await QuantosGruposAsync(client);

        var repetido = await client.PostAsJsonAsync("/api/v1/cards", Grupo("Segundo", new { kind = 1, lastFour = "5678" }));
        repetido.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await repetido.Content.ReadAsStringAsync()).Should().Contain("final-duplicado");

        var numeroInteiro = await client.PostAsJsonAsync("/api/v1/cards", Grupo("Terceiro", new { kind = 1, lastFour = "4532015112830366" }));
        numeroInteiro.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await QuantosGruposAsync(client)).Should().Be(antes);
    }

    [Fact]
    public async Task Titular_com_debito_em_conta_de_outro_grupo_e_proibido_e_nada_e_criado()
    {
        var outro = await NovoUsuarioAsync();
        var contaAlheia = (await JsonAsync(await outro.PostAsJsonAsync("/api/v1/accounts",
            new { name = "Alheia", type = 1, initialBalance = 0m }))).GetProperty("id").GetGuid();

        var client = await NovoUsuarioAsync();
        var response = await client.PostAsJsonAsync("/api/v1/cards", Grupo("Grupo", new { kind = 1, lastFour = "9999", accountId = contaAlheia }));
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await QuantosGruposAsync(client)).Should().Be(0);
    }
}
