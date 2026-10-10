using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using MeuFinanceiro.Application.Cards;
using MeuFinanceiro.Domain.Enums;
using Xunit;

namespace MeuFinanceiro.Application.IntegrationTests;

/// <summary>Cartão de débito, só de débito ou múltiplo (docs/04). Números e nomes fictícios.</summary>
public class DebitCardsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public DebitCardsTests(CustomWebApplicationFactory factory) => _factory = factory;

    // ---------------- sugestão do cartão de débito ----------------

    private sealed record Card(string Final, CardBrand? Brand);

    [Fact]
    [Trait("Category", "Unit")]
    public void Sugere_o_cartao_de_debito_so_quando_ha_um_da_bandeira()
    {
        var um = new[] { new Card("1111", CardBrand.Maestro), new Card("2222", CardBrand.Visa) };
        CardBrands.PickDebitCard(um, c => c.Brand, CardBrand.Mastercard)!.Final.Should().Be("1111");

        var dois = new[] { new Card("1111", CardBrand.Maestro), new Card("3333", CardBrand.Mastercard) };
        CardBrands.PickDebitCard(dois, c => c.Brand, CardBrand.Mastercard).Should().BeNull();

        CardBrands.PickDebitCard(um, c => c.Brand, CardBrand.Elo).Should().BeNull();
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData(CardBrand.Maestro, CardBrand.Mastercard, true)]
    [InlineData(CardBrand.VisaElectron, CardBrand.Visa, true)]
    [InlineData(CardBrand.EloDebito, CardBrand.Elo, true)]
    [InlineData(CardBrand.Maestro, CardBrand.Visa, false)]
    public void Bandeira_de_debito_e_da_familia_da_de_credito(CardBrand a, CardBrand b, bool mesma)
    {
        CardBrands.SameFamily(a, b).Should().Be(mesma);
    }

    // ---------------- endpoint ----------------

    private async Task<HttpClient> NovoUsuarioAsync()
    {
        var id = Guid.NewGuid().ToString("N")[..12];
        var (client, _) = await AuthTestHelpers.RegisterConfirmAndLoginAsync(
            _factory, "Usuário Débito", $"debito{id}", $"debito-{id}@teste.com", "SenhaForte!123");
        return client;
    }

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.Should().BeTrue(body);
        return body.Length == 0 ? default : JsonDocument.Parse(body).RootElement.Clone();
    }

    private static async Task ShouldBeRuleAsync(HttpResponseMessage response, string rule)
    {
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, body);
        JsonDocument.Parse(body).RootElement.GetProperty("type").GetString().Should().EndWith("/" + rule);
    }

    private static async Task<Guid> CriarContaAsync(HttpClient client) =>
        (await JsonAsync(await client.PostAsJsonAsync("/api/v1/accounts",
            new { name = $"Conta {Guid.NewGuid():N}"[..20], bank = "Banco Ficticio", type = 1, initialBalance = 0m })))
        .GetProperty("id").GetGuid();

    private static async Task<Guid> CriarGrupoAsync(HttpClient client) =>
        (await JsonAsync(await client.PostAsJsonAsync("/api/v1/cards",
            new { name = $"Grupo {Guid.NewGuid():N}"[..20], brand = "Mastercard", closingDay = 2, dueDay = 10 })))
        .GetProperty("id").GetGuid();

    private static async Task<List<JsonElement>> DaContaAsync(HttpClient client, Guid conta) =>
        (await JsonAsync(await client.GetAsync($"/api/v1/accounts/{conta}/debit-cards"))).EnumerateArray().ToList();

    private static async Task<List<JsonElement>> DoGrupoAsync(HttpClient client, Guid grupo) =>
        (await JsonAsync(await client.GetAsync($"/api/v1/cards/{grupo}/issued-cards"))).EnumerateArray().ToList();

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Cartao_so_de_debito_tem_bandeira_propria_inclusive_de_debito()
    {
        var client = await NovoUsuarioAsync();
        var conta = await CriarContaAsync(client);

        var cartao = await JsonAsync(await client.PostAsJsonAsync($"/api/v1/accounts/{conta}/debit-cards",
            new { kind = 1, lastFour = "1111", firstDigits = "5502", brand = "Maestro" }));
        cartao.GetProperty("function").GetInt32().Should().Be((int)IssuedCardFunction.Debito);
        cartao.GetProperty("brand").GetString().Should().Be("Maestro");
        cartao.GetProperty("creditCardId").ValueKind.Should().Be(JsonValueKind.Null);
        // Dígitos de Mastercard num Maestro: mesma família, sem aviso.
        cartao.GetProperty("brandMismatch").GetBoolean().Should().BeFalse();

        (await DaContaAsync(client, conta)).Should().ContainSingle();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Multiplo_aparece_no_grupo_e_na_conta_e_cancelar_vale_para_os_dois()
    {
        var client = await NovoUsuarioAsync();
        var conta = await CriarContaAsync(client);
        var grupo = await CriarGrupoAsync(client);

        var cartao = await JsonAsync(await client.PostAsJsonAsync($"/api/v1/cards/{grupo}/issued-cards",
            new { kind = 1, lastFour = "2222", accountId = conta, brand = "Visa" }));
        cartao.GetProperty("function").GetInt32().Should().Be((int)IssuedCardFunction.Multiplo);
        // A bandeira do crédito é a do grupo; a que veio no pedido é ignorada.
        cartao.GetProperty("brand").GetString().Should().Be("Mastercard");

        (await DoGrupoAsync(client, grupo)).Single().GetProperty("accountName").GetString().Should().NotBeNullOrEmpty();
        (await DaContaAsync(client, conta)).Single().GetProperty("creditCardName").GetString().Should().NotBeNullOrEmpty();

        await JsonAsync(await client.PostAsync($"/api/v1/issued-cards/{cartao.GetProperty("id").GetGuid()}/cancel", null));
        (await DoGrupoAsync(client, grupo)).Single().GetProperty("status").GetInt32().Should().Be((int)IssuedCardStatus.Cancelado);
        (await DaContaAsync(client, conta)).Single().GetProperty("status").GetInt32().Should().Be((int)IssuedCardStatus.Cancelado);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Tirar_o_debito_do_multiplo_o_deixa_so_de_credito_mas_so_de_debito_nao_fica_sem_conta()
    {
        var client = await NovoUsuarioAsync();
        var conta = await CriarContaAsync(client);
        var grupo = await CriarGrupoAsync(client);
        var multiplo = (await JsonAsync(await client.PostAsJsonAsync($"/api/v1/cards/{grupo}/issued-cards",
            new { kind = 1, lastFour = "3333", accountId = conta }))).GetProperty("id").GetGuid();

        var editado = await JsonAsync(await client.PutAsJsonAsync($"/api/v1/issued-cards/{multiplo}", new { kind = 1, lastFour = "3333" }));
        editado.GetProperty("function").GetInt32().Should().Be((int)IssuedCardFunction.Credito);
        (await DaContaAsync(client, conta)).Should().BeEmpty();

        var debito = (await JsonAsync(await client.PostAsJsonAsync($"/api/v1/accounts/{conta}/debit-cards",
            new { kind = 1, lastFour = "4444" }))).GetProperty("id").GetGuid();
        await ShouldBeRuleAsync(await client.PutAsJsonAsync($"/api/v1/issued-cards/{debito}", new { kind = 1, lastFour = "4444" }),
            "cartao-sem-funcao");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Final_do_debito_conta_na_unicidade_com_os_de_credito()
    {
        var client = await NovoUsuarioAsync();
        var conta = await CriarContaAsync(client);
        var grupo = await CriarGrupoAsync(client);
        await JsonAsync(await client.PostAsJsonAsync($"/api/v1/cards/{grupo}/issued-cards", new { kind = 1, lastFour = "5555" }));

        await ShouldBeRuleAsync(await client.PostAsJsonAsync($"/api/v1/accounts/{conta}/debit-cards", new { kind = 1, lastFour = "5555" }),
            "final-duplicado");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Substituir_o_multiplo_mantem_as_duas_funcoes()
    {
        var client = await NovoUsuarioAsync();
        var conta = await CriarContaAsync(client);
        var grupo = await CriarGrupoAsync(client);
        var antigo = (await JsonAsync(await client.PostAsJsonAsync($"/api/v1/cards/{grupo}/issued-cards",
            new { kind = 1, lastFour = "6666", accountId = conta }))).GetProperty("id").GetGuid();

        var novo = await JsonAsync(await client.PostAsJsonAsync($"/api/v1/issued-cards/{antigo}/replace",
            new { kind = 1, lastFour = "6666", expiryMonth = 1, expiryYear = 2032 }));
        novo.GetProperty("function").GetInt32().Should().Be((int)IssuedCardFunction.Multiplo);
        novo.GetProperty("accountId").GetGuid().Should().Be(conta);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Excluir_o_grupo_mantem_o_multiplo_como_debito_e_excluir_a_conta_mantem_como_credito()
    {
        var client = await NovoUsuarioAsync();
        var conta = await CriarContaAsync(client);
        var grupo = await CriarGrupoAsync(client);
        await JsonAsync(await client.PostAsJsonAsync($"/api/v1/cards/{grupo}/issued-cards", new { kind = 1, lastFour = "7777", accountId = conta }));
        await JsonAsync(await client.PostAsJsonAsync($"/api/v1/cards/{grupo}/issued-cards", new { kind = 2, lastFour = "7778" }));

        (await client.DeleteAsync($"/api/v1/cards/{grupo}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var restantes = await DaContaAsync(client, conta);
        restantes.Should().ContainSingle();
        restantes[0].GetProperty("function").GetInt32().Should().Be((int)IssuedCardFunction.Debito);
        // Herdou a bandeira do grupo que deixou de existir.
        restantes[0].GetProperty("brand").GetString().Should().Be("Mastercard");

        // E o contrário: com a conta excluída, o múltiplo segue como crédito; o só de débito sai.
        var conta2 = await CriarContaAsync(client);
        var grupo2 = await CriarGrupoAsync(client);
        await JsonAsync(await client.PostAsJsonAsync($"/api/v1/cards/{grupo2}/issued-cards", new { kind = 1, lastFour = "8888", accountId = conta2 }));
        await JsonAsync(await client.PostAsJsonAsync($"/api/v1/accounts/{conta2}/debit-cards", new { kind = 1, lastFour = "8889" }));

        (await client.DeleteAsync($"/api/v1/accounts/{conta2}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var doGrupo = await DoGrupoAsync(client, grupo2);
        doGrupo.Should().ContainSingle();
        doGrupo[0].GetProperty("function").GetInt32().Should().Be((int)IssuedCardFunction.Credito);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Conta_de_outro_grupo_nao_recebe_cartao_nem_e_listada()
    {
        var dono = await NovoUsuarioAsync();
        var conta = await CriarContaAsync(dono);

        var outro = await NovoUsuarioAsync();
        (await outro.GetAsync($"/api/v1/accounts/{conta}/debit-cards")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await outro.PostAsJsonAsync($"/api/v1/accounts/{conta}/debit-cards", new { kind = 1, lastFour = "9090" }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Nem como débito de um cartão de crédito do outro.
        var grupoDoOutro = await CriarGrupoAsync(outro);
        (await outro.PostAsJsonAsync($"/api/v1/cards/{grupoDoOutro}/issued-cards", new { kind = 1, lastFour = "9091", accountId = conta }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
