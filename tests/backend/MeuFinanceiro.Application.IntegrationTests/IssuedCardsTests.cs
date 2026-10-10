using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using MeuFinanceiro.Application.Cards;
using MeuFinanceiro.Application.Common.Exceptions;
using MeuFinanceiro.Domain.Enums;
using Xunit;

namespace MeuFinanceiro.Application.IntegrationTests;

/// <summary>Cartões de um grupo e bandeira (docs/04). Números e nomes fictícios.</summary>
public class IssuedCardsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public IssuedCardsTests(CustomWebApplicationFactory factory) => _factory = factory;

    // ---------------- bandeira ----------------

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("Visa", CardBrand.Visa)]
    [InlineData("master", CardBrand.Mastercard)]
    [InlineData("Mastercard", CardBrand.Mastercard)]
    [InlineData("AMEX", CardBrand.AmericanExpress)]
    [InlineData("American Express", CardBrand.AmericanExpress)]
    [InlineData("elo", CardBrand.Elo)]
    [InlineData("Outra", CardBrand.Outra)]
    public void Bandeira_aceita_nome_e_apelido(string raw, CardBrand esperada)
    {
        CardBrands.ParseForCreditGroup(raw).Should().Be(esperada);
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("Banco Qualquer", "bandeira-invalida")]
    [InlineData("3", "bandeira-invalida")]          // número do enum não vale como bandeira
    [InlineData("Maestro", "bandeira-de-debito")]
    public void Bandeira_desconhecida_ou_de_debito_e_recusada(string raw, string regra)
    {
        var act = () => CardBrands.ParseForCreditGroup(raw);
        act.Should().Throw<BusinessRuleException>().Which.ErrorType.Should().Be(regra);
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("4532", CardBrand.Visa)]
    [InlineData("453211", CardBrand.Visa)]
    [InlineData("401178", CardBrand.Elo)]        // Elo dentro do "4" da Visa
    [InlineData("5502", CardBrand.Mastercard)]
    [InlineData("2223", CardBrand.Mastercard)]
    [InlineData("3714", CardBrand.AmericanExpress)]
    [InlineData("606282", CardBrand.Hipercard)]
    [InlineData("3530", CardBrand.Jcb)]
    [InlineData("6011", CardBrand.Discover)]
    [InlineData("6212", CardBrand.UnionPay)]
    public void Primeiros_digitos_sugerem_a_bandeira(string digitos, CardBrand esperada)
    {
        CardBrands.SuggestFromDigits(digitos).Should().Be(esperada);
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("4011")]    // Visa ou Elo (401178): na dúvida, nada
    [InlineData("3841")]    // Diners ou Hipercard (384100)
    [InlineData("6500")]    // Discover ou Elo (650031…)
    [InlineData("9999")]
    [InlineData(null)]
    public void Sem_certeza_nao_sugere(string? digitos)
    {
        CardBrands.SuggestFromDigits(digitos).Should().BeNull();
    }

    // ---------------- endpoint ----------------

    private async Task<HttpClient> NovoUsuarioAsync()
    {
        var id = Guid.NewGuid().ToString("N")[..12];
        var (client, _) = await AuthTestHelpers.RegisterConfirmAndLoginAsync(
            _factory, "Usuário Cartões", $"cartoes{id}", $"cartoes-{id}@teste.com", "SenhaForte!123");
        return client;
    }

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.Should().BeTrue(body);
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static async Task ShouldBeRuleAsync(HttpResponseMessage response, string rule)
    {
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, body);
        JsonDocument.Parse(body).RootElement.GetProperty("type").GetString().Should().EndWith("/" + rule);
    }

    private static async Task<Guid> CriarGrupoAsync(HttpClient client, string brand = "Visa") =>
        (await JsonAsync(await client.PostAsJsonAsync("/api/v1/cards",
            new { name = $"Grupo {Guid.NewGuid():N}"[..20], brand, closingDay = 2, dueDay = 10, limit = 5000m })))
        .GetProperty("id").GetGuid();

    private static object Cartao(string final, int kind = 1, string? inicio = null, int? mes = null, int? ano = null, string? nome = null) =>
        new { kind, lastFour = final, firstDigits = inicio, expiryMonth = mes, expiryYear = ano, holderName = nome };

    private static Task<HttpResponseMessage> AdicionarAsync(HttpClient client, Guid grupo, object cartao) =>
        client.PostAsJsonAsync($"/api/v1/cards/{grupo}/issued-cards", cartao);

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Grupo_guarda_a_bandeira_pelo_nome_e_aceita_o_apelido_antigo()
    {
        var client = await NovoUsuarioAsync();
        var response = await client.PostAsJsonAsync("/api/v1/cards", new { name = "Antigo", brand = "Master", closingDay = 1, dueDay = 8 });
        (await JsonAsync(response)).GetProperty("brand").GetString().Should().Be("Mastercard");

        await ShouldBeRuleAsync(await client.PostAsJsonAsync("/api/v1/cards",
            new { name = "Inválido", brand = "Banco X", closingDay = 1, dueDay = 8 }), "bandeira-invalida");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Grupo_tem_titular_adicional_e_virtual_e_aceita_dois_titulares()
    {
        var client = await NovoUsuarioAsync();
        var grupo = await CriarGrupoAsync(client);

        (await AdicionarAsync(client, grupo, Cartao("1111", 1, "4532", 12, 30, "PORTADOR FICTICIO"))).StatusCode.Should().Be(HttpStatusCode.Created);
        // Dois titulares ativos no mesmo grupo: o banco emite um novo e o antigo segue valendo.
        (await AdicionarAsync(client, grupo, Cartao("2222", 1))).StatusCode.Should().Be(HttpStatusCode.Created);
        (await AdicionarAsync(client, grupo, Cartao("3333", 2, nome: "ADICIONAL FICTICIO"))).StatusCode.Should().Be(HttpStatusCode.Created);
        (await AdicionarAsync(client, grupo, Cartao("4444", 3))).StatusCode.Should().Be(HttpStatusCode.Created);

        var lista = (await JsonAsync(await client.GetAsync($"/api/v1/cards/{grupo}/issued-cards"))).EnumerateArray().ToList();
        lista.Should().HaveCount(4);
        var primeiro = lista.Single(c => c.GetProperty("lastFour").GetString() == "1111");
        primeiro.GetProperty("expiryYear").GetInt32().Should().Be(2030);
        primeiro.GetProperty("suggestedBrand").GetString().Should().Be("Visa");
        primeiro.GetProperty("brandMismatch").GetBoolean().Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Numero_completo_nunca_e_aceito()
    {
        var client = await NovoUsuarioAsync();
        var grupo = await CriarGrupoAsync(client);

        await ShouldBeRuleAsync(await AdicionarAsync(client, grupo, Cartao("4532015112830366")), "numero-completo-nao-guardado");
        await ShouldBeRuleAsync(await AdicionarAsync(client, grupo, Cartao("0366", inicio: "45320151")), "numero-completo-nao-guardado");
        await ShouldBeRuleAsync(await AdicionarAsync(client, grupo, Cartao("366")), "final-invalido");
        await ShouldBeRuleAsync(await AdicionarAsync(client, grupo, Cartao("0366", mes: 13, ano: 2030)), "validade-invalida");
        await ShouldBeRuleAsync(await AdicionarAsync(client, grupo, Cartao("0366", mes: 5)), "validade-incompleta");

        // Nem por um campo a mais no JSON: o contrato não tem onde guardar.
        var extra = await AdicionarAsync(client, grupo, new { kind = 1, lastFour = "0366", number = "4532015112830366", cvv = "123" });
        var json = await JsonAsync(extra);
        json.TryGetProperty("number", out _).Should().BeFalse();
        json.TryGetProperty("cvv", out _).Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Final_repetido_entre_ativos_e_recusado_ate_cancelar()
    {
        var client = await NovoUsuarioAsync();
        var grupoA = await CriarGrupoAsync(client);
        var grupoB = await CriarGrupoAsync(client);
        var primeiro = await JsonAsync(await AdicionarAsync(client, grupoA, Cartao("5555")));

        // Também entre grupos: o final é o que a fatura imprime, e a importação acha o grupo por ele.
        await ShouldBeRuleAsync(await AdicionarAsync(client, grupoB, Cartao("5555")), "final-duplicado");

        (await client.PostAsync($"/api/v1/issued-cards/{primeiro.GetProperty("id").GetGuid()}/cancel", null)).IsSuccessStatusCode.Should().BeTrue();
        (await AdicionarAsync(client, grupoB, Cartao("5555"))).StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Final_igual_em_outro_grupo_de_usuarios_e_permitido()
    {
        var a = await NovoUsuarioAsync();
        var b = await NovoUsuarioAsync();
        (await AdicionarAsync(a, await CriarGrupoAsync(a), Cartao("6666"))).StatusCode.Should().Be(HttpStatusCode.Created);
        (await AdicionarAsync(b, await CriarGrupoAsync(b), Cartao("6666"))).StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Substituir_mantendo_o_numero_marca_o_antigo_e_aponta_o_novo()
    {
        var client = await NovoUsuarioAsync();
        var grupo = await CriarGrupoAsync(client);
        var antigo = await JsonAsync(await AdicionarAsync(client, grupo, Cartao("7777", mes: 1, ano: 2020)));
        var antigoId = antigo.GetProperty("id").GetGuid();
        antigo.GetProperty("isExpired").GetBoolean().Should().BeTrue();

        // Renovação: mesmo final, validade nova.
        var novo = await JsonAsync(await client.PostAsJsonAsync($"/api/v1/issued-cards/{antigoId}/replace", Cartao("7777", mes: 1, ano: 2031)));
        novo.GetProperty("status").GetInt32().Should().Be((int)IssuedCardStatus.Ativo);
        novo.GetProperty("isExpired").GetBoolean().Should().BeFalse();

        var lista = (await JsonAsync(await client.GetAsync($"/api/v1/cards/{grupo}/issued-cards"))).EnumerateArray().ToList();
        var velho = lista.Single(c => c.GetProperty("id").GetGuid() == antigoId);
        velho.GetProperty("status").GetInt32().Should().Be((int)IssuedCardStatus.Substituido);
        velho.GetProperty("replacedById").GetGuid().Should().Be(novo.GetProperty("id").GetGuid());

        // Substituído não se substitui de novo.
        await ShouldBeRuleAsync(await client.PostAsJsonAsync($"/api/v1/issued-cards/{antigoId}/replace", Cartao("8888")), "cartao-nao-ativo");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Digitos_de_outra_bandeira_que_a_do_grupo_viram_aviso_e_nao_bloqueio()
    {
        var client = await NovoUsuarioAsync();
        var grupo = await CriarGrupoAsync(client, "Visa");
        var response = await AdicionarAsync(client, grupo, Cartao("9999", inicio: "5502"));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var cartao = await JsonAsync(response);
        cartao.GetProperty("suggestedBrand").GetString().Should().Be("Mastercard");
        cartao.GetProperty("brandMismatch").GetBoolean().Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Cartoes_de_outro_grupo_nao_sao_vistos_nem_alterados()
    {
        var dono = await NovoUsuarioAsync();
        var grupo = await CriarGrupoAsync(dono);
        var cartao = await JsonAsync(await AdicionarAsync(dono, grupo, Cartao("1212")));
        var id = cartao.GetProperty("id").GetGuid();

        var outro = await NovoUsuarioAsync();
        (await outro.GetAsync($"/api/v1/cards/{grupo}/issued-cards")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await AdicionarAsync(outro, grupo, Cartao("3434"))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await outro.PutAsJsonAsync($"/api/v1/issued-cards/{id}", Cartao("1213"))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await outro.PostAsync($"/api/v1/issued-cards/{id}/cancel", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await outro.DeleteAsync($"/api/v1/issued-cards/{id}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Excluir_o_substituto_solta_o_substituido()
    {
        var client = await NovoUsuarioAsync();
        var grupo = await CriarGrupoAsync(client);
        var antigoId = (await JsonAsync(await AdicionarAsync(client, grupo, Cartao("1313")))).GetProperty("id").GetGuid();
        var novoId = (await JsonAsync(await client.PostAsJsonAsync($"/api/v1/issued-cards/{antigoId}/replace", Cartao("1414"))))
            .GetProperty("id").GetGuid();

        (await client.DeleteAsync($"/api/v1/issued-cards/{novoId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var velho = (await JsonAsync(await client.GetAsync($"/api/v1/cards/{grupo}/issued-cards"))).EnumerateArray().Single();
        velho.GetProperty("replacedById").ValueKind.Should().Be(JsonValueKind.Null);
    }
}
