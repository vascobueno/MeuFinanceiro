using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using MeuFinanceiro.Application.Cards;
using MeuFinanceiro.Domain.Enums;
using Xunit;

namespace MeuFinanceiro.Application.IntegrationTests;

/// <summary>O cartão usado em cada lançamento, no crédito e no débito (docs/06). Dados sintéticos.</summary>
public class TransactionIssuedCardTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public TransactionIssuedCardTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("COMPRA CARTAO DEB MC 200652 LOJA", CardBrand.Mastercard)]
    [InlineData("COMPRA CARTAO DEB VS LOJA", CardBrand.Visa)]
    [InlineData("COMPRA ELO DEB LOJA", CardBrand.Elo)]
    [InlineData("COMPRA CARTAO DEB LOJA MCDONALDS", null)]   // "MC" só como palavra
    [InlineData("COMPRA CARTAO DEB LOJA", null)]
    public void Bandeira_da_compra_no_debito_pela_descricao(string descricao, CardBrand? esperada)
    {
        CardBrands.FromStatementText(descricao).Should().Be(esperada);
    }

    private async Task<HttpClient> NovoUsuarioAsync()
    {
        var id = Guid.NewGuid().ToString("N")[..12];
        var (client, _) = await AuthTestHelpers.RegisterConfirmAndLoginAsync(
            _factory, "Usuário Cartão Usado", $"usado{id}", $"usado-{id}@teste.com", "SenhaForte!123");
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

    private static async Task<(Guid grupo, Guid cartao)> GrupoAsync(HttpClient client, string final, Guid? debitoEm = null)
    {
        var grupo = (await JsonAsync(await client.PostAsJsonAsync("/api/v1/cards",
            new { name = $"Grupo {Guid.NewGuid():N}"[..20], brand = "Mastercard", closingDay = 2, dueDay = 10 }))).GetProperty("id").GetGuid();
        var cartao = (await JsonAsync(await client.PostAsJsonAsync($"/api/v1/cards/{grupo}/issued-cards",
            new { kind = 1, lastFour = final, accountId = debitoEm }))).GetProperty("id").GetGuid();
        return (grupo, cartao);
    }

    private static async Task<Guid> ContaAsync(HttpClient client) =>
        (await JsonAsync(await client.PostAsJsonAsync("/api/v1/accounts",
            new { name = $"Conta {Guid.NewGuid():N}"[..20], type = 1, initialBalance = 1000m }))).GetProperty("id").GetGuid();

    private static object NoCartao(Guid grupo, Guid? cartao, object? installment = null) => new
    {
        transactionDate = "2026-08-10", description = "Loja Ficticia", creditCardId = grupo, type = 2, direction = 1, amount = 90m,
        issuedCardId = cartao, installment,
    };

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Lancamento_no_cartao_guarda_o_cartao_usado_e_as_parcelas_herdam()
    {
        var client = await NovoUsuarioAsync();
        var (grupo, cartao) = await GrupoAsync(client, "9072");

        var simples = (await JsonAsync(await client.PostAsJsonAsync("/api/v1/transactions", NoCartao(grupo, cartao)))).EnumerateArray().Single();
        simples.GetProperty("issuedCardLastFour").GetString().Should().Be("9072");
        simples.GetProperty("issuedCardKind").GetInt32().Should().Be((int)IssuedCardKind.Titular);

        var parcelas = (await JsonAsync(await client.PostAsJsonAsync("/api/v1/transactions", NoCartao(grupo, cartao,
            new { mode = 1, amount = 90m, installmentCount = 3, firstDueDate = "2026-08-10" })))).EnumerateArray().ToList();
        parcelas.Should().HaveCount(3).And.OnlyContain(p => p.GetProperty("issuedCardId").GetGuid() == cartao);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Cartao_de_outra_fatura_ou_sem_debito_na_conta_e_recusado()
    {
        var client = await NovoUsuarioAsync();
        var (grupoA, _) = await GrupoAsync(client, "1111");
        var (_, cartaoB) = await GrupoAsync(client, "2222");
        await ShouldBeRuleAsync(await client.PostAsJsonAsync("/api/v1/transactions", NoCartao(grupoA, cartaoB)), "cartao-de-outra-fatura");

        var conta = await ContaAsync(client);
        await ShouldBeRuleAsync(await client.PostAsJsonAsync("/api/v1/transactions", new
        {
            transactionDate = "2026-08-10", description = "Débito", accountId = conta, type = 2, direction = 1, amount = 20m, issuedCardId = cartaoB,
        }), "cartao-sem-debito-na-conta");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Cartao_de_debito_vale_no_lancamento_da_conta()
    {
        var client = await NovoUsuarioAsync();
        var conta = await ContaAsync(client);
        var (_, multiplo) = await GrupoAsync(client, "3333", debitoEm: conta);

        var lancamento = (await JsonAsync(await client.PostAsJsonAsync("/api/v1/transactions", new
        {
            transactionDate = "2026-08-10", description = "Padaria Ficticia", accountId = conta, type = 2, direction = 1, amount = 12m, issuedCardId = multiplo,
        }))).EnumerateArray().Single();
        lancamento.GetProperty("issuedCardLastFour").GetString().Should().Be("3333");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Cartao_de_outro_grupo_de_usuarios_e_proibido()
    {
        var outro = await NovoUsuarioAsync();
        var (_, alheio) = await GrupoAsync(outro, "4444");

        var client = await NovoUsuarioAsync();
        var (grupo, _) = await GrupoAsync(client, "5555");
        (await client.PostAsJsonAsync("/api/v1/transactions", NoCartao(grupo, alheio))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Editar_sem_cartao_tira_a_indicacao()
    {
        var client = await NovoUsuarioAsync();
        var (grupo, cartao) = await GrupoAsync(client, "6666");
        var id = (await JsonAsync(await client.PostAsJsonAsync("/api/v1/transactions", NoCartao(grupo, cartao)))).EnumerateArray().Single()
            .GetProperty("id").GetGuid();

        var editado = await JsonAsync(await client.PutAsJsonAsync($"/api/v1/transactions/{id}", new
        {
            transactionDate = "2026-08-10", description = "Loja Ficticia", creditCardId = grupo, amount = 90m, direction = 1, status = 2,
        }));
        editado.GetProperty("issuedCardId").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Aceitar_o_item_da_fatura_leva_o_cartao_da_secao_para_o_lancamento()
    {
        var client = await NovoUsuarioAsync();
        var (grupo, cartao) = await GrupoAsync(client, "7777");
        await JsonAsync(await client.PostAsJsonAsync("/api/v1/transactions", new
        {
            transactionDate = "2026-08-06", description = "Loja Ficticia A", creditCardId = grupo, type = 2, direction = 1, amount = 58.50m,
        }));

        var lido = await JsonAsync(await client.PostAsJsonAsync("/api/v1/card-invoices/parse", new
        {
            text = "PORTADOR FICTICIO - 5480 XXXX XXXX 7777\n06/08 LOJA FICTICIA A 58,50", closingDate = "2026-09-02",
        }));
        await JsonAsync(await client.PostAsJsonAsync("/api/v1/card-invoices/import", new
        {
            creditCardId = grupo, closingDate = "2026-09-02", dueDate = "2026-09-10", totalAmount = 58.50m, items = lido.GetProperty("items"),
        }));
        var r = (await JsonAsync(await client.GetAsync($"/api/v1/reconciliations/card?creditCardId={grupo}"))).EnumerateArray().Single();
        await JsonAsync(await client.PostAsync($"/api/v1/reconciliations/card/{r.GetProperty("id").GetGuid()}/accept", null));

        var lancamento = (await JsonAsync(await client.GetAsync($"/api/v1/transactions?creditCardId={grupo}"))).GetProperty("items").EnumerateArray().Single();
        lancamento.GetProperty("issuedCardId").GetGuid().Should().Be(cartao);
    }

    private static async Task<JsonElement> DebitoConciliadoAsync(HttpClient client, Guid conta)
    {
        await JsonAsync(await client.PostAsJsonAsync("/api/v1/transactions", new
        {
            transactionDate = "2023-01-02", description = "Loja Ficticia", accountId = conta, type = 2, direction = 1, amount = 18m,
        }));
        var lido = await JsonAsync(await client.PostAsJsonAsync("/api/v1/bank-statements/parse", new
        {
            text = "02/01 COMPRA CARTAO DEB MC 200652 18,00-\n02/01 LOJA FICTICIA", referenceStart = "2023-01-01", referenceEnd = "2023-01-31",
        }));
        await JsonAsync(await client.PostAsJsonAsync("/api/v1/bank-statements/import", new
        {
            accountId = conta, referenceStart = "2023-01-01", referenceEnd = "2023-01-31", openingBalance = 18m, closingBalance = 0m,
            items = lido.GetProperty("items"),
        }));
        var r = (await JsonAsync(await client.GetAsync($"/api/v1/reconciliations/bank?accountId={conta}"))).EnumerateArray().Single();
        await JsonAsync(await client.PostAsync($"/api/v1/reconciliations/bank/{r.GetProperty("id").GetGuid()}/accept", null));
        return (await JsonAsync(await client.GetAsync($"/api/v1/transactions?accountId={conta}"))).GetProperty("items").EnumerateArray().Single();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Compra_no_debito_conciliada_ganha_o_unico_cartao_da_bandeira()
    {
        var client = await NovoUsuarioAsync();
        var conta = await ContaAsync(client);
        var maestro = (await JsonAsync(await client.PostAsJsonAsync($"/api/v1/accounts/{conta}/debit-cards",
            new { kind = 1, lastFour = "8888", brand = "Maestro" }))).GetProperty("id").GetGuid();
        await JsonAsync(await client.PostAsJsonAsync($"/api/v1/accounts/{conta}/debit-cards", new { kind = 1, lastFour = "8889", brand = "Visa" }));

        var lancamento = await DebitoConciliadoAsync(client, conta);
        lancamento.GetProperty("issuedCardId").GetGuid().Should().Be(maestro);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Com_dois_cartoes_da_bandeira_nao_chuta()
    {
        var client = await NovoUsuarioAsync();
        var conta = await ContaAsync(client);
        await JsonAsync(await client.PostAsJsonAsync($"/api/v1/accounts/{conta}/debit-cards", new { kind = 1, lastFour = "9998", brand = "Maestro" }));
        // O múltiplo de um grupo Mastercard também é da família.
        await GrupoAsync(client, "9999", debitoEm: conta);

        var lancamento = await DebitoConciliadoAsync(client, conta);
        lancamento.GetProperty("issuedCardId").ValueKind.Should().Be(JsonValueKind.Null);
    }
}
