using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using MeuFinanceiro.Application.Reconciliation.Import;
using Xunit;

namespace MeuFinanceiro.Application.IntegrationTests;

/// <summary>
/// Depois da importação da fatura (docs/06, docs/08): datas lidas, "Lançar" item sem par, valor
/// zero ignorado e câmbio no lançamento. Dados sintéticos.
/// </summary>
public class InvoiceFollowUpTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public InvoiceFollowUpTests(CustomWebApplicationFactory factory) => _factory = factory;

    private static readonly DateOnly Fechamento = new(2026, 9, 2);

    // ---------------- datas lidas da fatura ----------------

    [Fact]
    [Trait("Category", "Unit")]
    public void Le_fechamento_vencimento_e_proximo_fechamento()
    {
        var r = InvoiceTextReader.Read("""
            Data de fechamento: 01/09/2026
            Vencimento 10/09
            Próximo fechamento 02/10
            06/08 LOJA FICTICIA 58,50
            """, Fechamento);

        r.ClosingDateFromText.Should().Be(new DateOnly(2026, 9, 1));
        r.DueDateFromText.Should().Be(new DateOnly(2026, 9, 10));
        r.NextClosingDateFromText.Should().Be(new DateOnly(2026, 10, 2));
        r.Items.Should().ContainSingle();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Proximo_fechamento_em_dezembro_vira_o_ano()
    {
        var r = InvoiceTextReader.Read("Próximo fechamento 02/01", new DateOnly(2026, 12, 2));
        r.NextClosingDateFromText.Should().Be(new DateOnly(2027, 1, 2));
    }

    // ---------------- endpoint ----------------

    private async Task<HttpClient> NovoUsuarioAsync()
    {
        var id = Guid.NewGuid().ToString("N")[..12];
        var (client, _) = await AuthTestHelpers.RegisterConfirmAndLoginAsync(
            _factory, "Usuário Lançar", $"lancar{id}", $"lancar-{id}@teste.com", "SenhaForte!123");
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

    private static async Task<Guid> CriarGrupoAsync(HttpClient client) =>
        (await JsonAsync(await client.PostAsJsonAsync("/api/v1/cards",
            new { name = $"Grupo {Guid.NewGuid():N}"[..20], brand = "Visa", closingDay = 2, dueDay = 10 }))).GetProperty("id").GetGuid();

    private const string Texto = """
        PORTADOR FICTICIO - 4532 XXXX XXXX 1111
        10/08 PAGAMENTO DE FATURA -500,00
        12/08 ESTORNO LOJA FICTICIA -10,00
        21/08 SERVICO NO EXTERIOR 116,65 21,22
        COTAÇÃO DOLAR R$ 5,4970
        IOF DESPESA NO EXTERIOR 4,08
        25/08 ANUIDADE DIFERENCIADA 03/12 39,90
        02/09 SEGURO FICTICIO 08/12 0,00
        """;

    /// <summary>Importa o texto e devolve as conciliações por descrição.</summary>
    private static async Task<Dictionary<string, JsonElement>> ImportarAsync(HttpClient client, Guid grupo)
    {
        var lido = await JsonAsync(await client.PostAsJsonAsync("/api/v1/card-invoices/parse", new { text = Texto, closingDate = "2026-09-02" }));
        await JsonAsync(await client.PostAsJsonAsync("/api/v1/card-invoices/import", new
        {
            creditCardId = grupo, closingDate = "2026-09-02", dueDate = "2026-09-10", totalAmount = 0m,
            confirmUnbalanced = true, items = lido.GetProperty("items"),
        }));
        return (await JsonAsync(await client.GetAsync($"/api/v1/reconciliations/card?creditCardId={grupo}")))
            .EnumerateArray().ToDictionary(r => r.GetProperty("itemDescription").GetString()!, r => r);
    }

    private static Task<HttpResponseMessage> LancarAsync(HttpClient client, JsonElement r, object? corpo = null) =>
        client.PostAsJsonAsync($"/api/v1/reconciliations/card/{r.GetProperty("id").GetGuid()}/create-transaction", corpo ?? new { });

    private static async Task<List<JsonElement>> LancamentosDoCartaoAsync(HttpClient client, Guid grupo) =>
        (await JsonAsync(await client.GetAsync($"/api/v1/transactions?creditCardId={grupo}&pageSize=50")))
            .GetProperty("items").EnumerateArray().ToList();

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Item_de_valor_zero_entra_ignorado_e_nao_se_lanca()
    {
        var client = await NovoUsuarioAsync();
        var grupo = await CriarGrupoAsync(client);
        var itens = await ImportarAsync(client, grupo);

        var seguro = itens["SEGURO FICTICIO"];
        seguro.GetProperty("status").GetString().Should().Be("Ignored");
        await ShouldBeRuleAsync(await LancarAsync(client, seguro), "item-sem-valor");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Lancar_a_anuidade_cria_o_lancamento_no_cartao_ja_conciliado()
    {
        var client = await NovoUsuarioAsync();
        var grupo = await CriarGrupoAsync(client);
        var itens = await ImportarAsync(client, grupo);

        var r = await JsonAsync(await LancarAsync(client, itens["ANUIDADE DIFERENCIADA"]));
        r.GetProperty("status").GetString().Should().Be("ManuallyMatched");
        r.GetProperty("accepted").GetBoolean().Should().BeTrue();

        var lancamento = (await LancamentosDoCartaoAsync(client, grupo)).Single();
        lancamento.GetProperty("description").GetString().Should().Be("ANUIDADE DIFERENCIADA (03/12)");
        lancamento.GetProperty("amount").GetDecimal().Should().Be(39.90m);
        lancamento.GetProperty("transactionDate").GetString().Should().Be("2026-08-25");
        lancamento.GetProperty("id").GetGuid().Should().Be(r.GetProperty("financialTransactionId").GetGuid());

        // Já conciliado: lançar de novo é recusado.
        await ShouldBeRuleAsync(await LancarAsync(client, itens["ANUIDADE DIFERENCIADA"]), "conciliacao-ja-aceita");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Lancar_compra_no_exterior_leva_o_cambio_e_estorno_reduz_a_fatura()
    {
        var client = await NovoUsuarioAsync();
        var grupo = await CriarGrupoAsync(client);
        var itens = await ImportarAsync(client, grupo);

        await JsonAsync(await LancarAsync(client, itens["SERVICO NO EXTERIOR"]));
        await JsonAsync(await LancarAsync(client, itens["ESTORNO LOJA FICTICIA"]));

        var lancamentos = await LancamentosDoCartaoAsync(client, grupo);
        var exterior = lancamentos.Single(l => l.GetProperty("description").GetString() == "SERVICO NO EXTERIOR");
        var cambio = exterior.GetProperty("foreign");
        cambio.GetProperty("amount").GetDecimal().Should().Be(21.22m);
        cambio.GetProperty("currency").GetString().Should().Be("USD");
        cambio.GetProperty("exchangeRate").GetDecimal().Should().Be(5.4970m);

        var estorno = lancamentos.Single(l => l.GetProperty("description").GetString() == "ESTORNO LOJA FICTICIA");
        estorno.GetProperty("amount").GetDecimal().Should().Be(10m);
        estorno.GetProperty("direction").GetInt32().Should().Be(2); // crédito: reduz a fatura
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Pagamento_nao_se_lanca_pelo_item()
    {
        var client = await NovoUsuarioAsync();
        var grupo = await CriarGrupoAsync(client);
        var itens = await ImportarAsync(client, grupo);
        await ShouldBeRuleAsync(await LancarAsync(client, itens["PAGAMENTO DE FATURA"]), "item-de-pagamento");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Item_e_categoria_de_outro_grupo_sao_proibidos()
    {
        var dono = await NovoUsuarioAsync();
        var grupo = await CriarGrupoAsync(dono);
        var itens = await ImportarAsync(dono, grupo);

        var outro = await NovoUsuarioAsync();
        (await LancarAsync(outro, itens["ANUIDADE DIFERENCIADA"])).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var categoriaAlheia = (await JsonAsync(await outro.PostAsJsonAsync("/api/v1/categories", new { name = "Alheia", type = 2 })))
            .GetProperty("id").GetGuid();
        (await LancarAsync(dono, itens["IOF DESPESA NO EXTERIOR"], new { categoryId = categoriaAlheia }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Aceitar_a_sugestao_copia_o_cambio_para_o_lancamento_sem_cambio()
    {
        var client = await NovoUsuarioAsync();
        var grupo = await CriarGrupoAsync(client);
        // O usuário lançou a compra antes da fatura, sem dados de câmbio.
        await JsonAsync(await client.PostAsJsonAsync("/api/v1/transactions", new
        {
            transactionDate = "2026-08-21", description = "Servico no exterior", creditCardId = grupo, type = 2, direction = 1, amount = 116.65m,
        }));
        var itens = await ImportarAsync(client, grupo);
        var sugestao = itens["SERVICO NO EXTERIOR"];
        sugestao.GetProperty("financialTransactionId").ValueKind.Should().Be(JsonValueKind.String);

        await JsonAsync(await client.PostAsync($"/api/v1/reconciliations/card/{sugestao.GetProperty("id").GetGuid()}/accept", null));

        var lancamento = (await LancamentosDoCartaoAsync(client, grupo)).Single();
        lancamento.GetProperty("foreign").GetProperty("amount").GetDecimal().Should().Be(21.22m);
    }

    // ---------------- câmbio no lançamento ----------------

    private static object Lancamento(Guid grupo, object? foreign) => new
    {
        transactionDate = "2026-08-21", description = "Compra no exterior", creditCardId = grupo, type = 2, direction = 1, amount = 116.65m, foreign,
    };

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Lancamento_guarda_o_cambio_e_edicao_sem_ele_o_tira()
    {
        var client = await NovoUsuarioAsync();
        var grupo = await CriarGrupoAsync(client);

        var criado = (await JsonAsync(await client.PostAsJsonAsync("/api/v1/transactions", Lancamento(grupo,
            new { amount = 21.22m, currency = "usd", exchangeRate = 5.4970m, originalAmount = 19.50m, originalCurrency = "EUR" })))).EnumerateArray().Single();
        var cambio = criado.GetProperty("foreign");
        cambio.GetProperty("currency").GetString().Should().Be("USD");
        cambio.GetProperty("originalCurrency").GetString().Should().Be("EUR");

        var id = criado.GetProperty("id").GetGuid();
        var editado = await JsonAsync(await client.PutAsJsonAsync($"/api/v1/transactions/{id}", new
        {
            transactionDate = "2026-08-21", description = "Compra no exterior", creditCardId = grupo, amount = 116.65m, direction = 1, status = 2,
        }));
        editado.GetProperty("foreign").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Theory]
    [Trait("Category", "Integration")]
    [InlineData("BRL", 21.22, null, null, "moeda-invalida")]
    [InlineData("USD", 0, null, null, "valor-estrangeiro-invalido")]
    [InlineData("USD", 21.22, 19.50, null, "moeda-de-origem-incompleta")]
    public async Task Cambio_invalido_e_recusado(string moeda, double valor, double? origem, string? moedaOrigem, string regra)
    {
        var client = await NovoUsuarioAsync();
        var grupo = await CriarGrupoAsync(client);
        await ShouldBeRuleAsync(await client.PostAsJsonAsync("/api/v1/transactions", Lancamento(grupo,
            new { amount = (decimal)valor, currency = moeda, originalAmount = (decimal?)origem, originalCurrency = moedaOrigem })), regra);
    }
}
