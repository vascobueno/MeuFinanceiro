using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace MeuFinanceiro.Application.IntegrationTests;

/// <summary>
/// Conciliação da fatura importada com parcelas e com o pagamento da fatura anterior (docs/08).
///
/// A fatura lista a parcela com a data da COMPRA — "19/05 LOJA 04/06" na fatura de setembro —,
/// enquanto o lançamento da 4ª parcela está em agosto. Antes, a janela de 15 dias descartava a
/// parcela e o rótulo era comparado com Contains, que casava parcelas erradas.
/// </summary>
[Trait("Category", "Integration")]
public class CardInvoiceMatchingEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public CardInvoiceMatchingEndpointTests(CustomWebApplicationFactory factory) => _factory = factory;

    private static readonly DateOnly Compra = new(2026, 5, 19);
    private static readonly DateOnly Fechamento = new(2026, 9, 2);

    // ---------------- apoio ----------------

    private async Task<HttpClient> NovoUsuarioAsync()
    {
        var id = Guid.NewGuid().ToString("N")[..12];
        var (client, _) = await AuthTestHelpers.RegisterConfirmAndLoginAsync(
            _factory, "Usuário Fatura", $"fatura{id}", $"fatura-{id}@teste.com", "SenhaForte!123");
        return client;
    }

    private static string Iso(DateOnly d) => d.ToString("yyyy-MM-dd");

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();

    private static async Task<Guid> CriarAsync(HttpClient client, string path, object body)
    {
        var response = await client.PostAsJsonAsync(path, body);
        response.IsSuccessStatusCode.Should().BeTrue(await response.Content.ReadAsStringAsync());
        return (await JsonAsync(response)).GetProperty("id").GetGuid();
    }

    private static Task<Guid> CriarCartaoAsync(HttpClient client) => CriarAsync(client, "/api/v1/cards",
        new { name = $"Cartao {Guid.NewGuid():N}"[..20], brand = "Visa", closingDay = 2, dueDay = 10, limit = 10000m });

    private static Task<Guid> CriarContaAsync(HttpClient client) => CriarAsync(client, "/api/v1/accounts",
        new { name = $"Conta {Guid.NewGuid():N}"[..20], bank = "Banco Teste", type = 1, initialBalance = 5000m });

    /// <summary>Parcelamento no cartão; devolve o id do lançamento de cada parcela, pela ordem.</summary>
    private static async Task<List<Guid>> ParcelarAsync(HttpClient client, Guid cardId, string descricao, decimal parcela, int quantidade, DateOnly primeira)
    {
        var response = await client.PostAsJsonAsync("/api/v1/transactions", new
        {
            transactionDate = Iso(primeira),
            description = descricao,
            creditCardId = cardId,
            type = 2,
            direction = 1,
            amount = parcela,
            installment = new { mode = 2, amount = parcela, installmentCount = quantidade, firstDueDate = Iso(primeira) },
        });
        response.IsSuccessStatusCode.Should().BeTrue(await response.Content.ReadAsStringAsync());
        var lista = await JsonAsync(response);
        return lista.EnumerateArray().Select(t => t.GetProperty("id").GetGuid()).ToList();
    }

    private static async Task<Guid> GastarAsync(HttpClient client, Guid cardId, string descricao, decimal valor, DateOnly data)
    {
        var response = await client.PostAsJsonAsync("/api/v1/transactions", new
        {
            transactionDate = Iso(data), description = descricao, creditCardId = cardId, type = 2, direction = 1, amount = valor,
        });
        response.IsSuccessStatusCode.Should().BeTrue(await response.Content.ReadAsStringAsync());
        return (await JsonAsync(response)).EnumerateArray().Single().GetProperty("id").GetGuid();
    }

    private static async Task ImportarAsync(HttpClient client, Guid cardId, params object[] itens)
    {
        var response = await client.PostAsJsonAsync("/api/v1/card-invoices/import", new
        {
            creditCardId = cardId,
            closingDate = Iso(Fechamento),
            dueDate = Iso(Fechamento.AddDays(8)),
            totalAmount = 0m,
            confirmUnbalanced = true,
            items = itens,
        });
        response.IsSuccessStatusCode.Should().BeTrue(await response.Content.ReadAsStringAsync());
    }

    private static object Item(DateOnly data, string descricao, decimal valor, string? parcela = null) =>
        new { date = Iso(data), description = descricao, installmentLabel = parcela, amount = valor };

    private static async Task<List<JsonElement>> ConciliacoesAsync(HttpClient client, Guid cardId)
    {
        var response = await client.GetAsync($"/api/v1/reconciliations/card?creditCardId={cardId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await JsonAsync(response)).EnumerateArray().ToList();
    }

    private static Guid? Alvo(JsonElement r, string campo) =>
        r.GetProperty(campo).ValueKind == JsonValueKind.Null ? null : r.GetProperty(campo).GetGuid();

    // ---------------- parcelas ----------------

    [Fact]
    public async Task Parcela_listada_com_a_data_da_compra_deve_casar_com_a_parcela_certa_do_plano()
    {
        var client = await NovoUsuarioAsync();
        var cardId = await CriarCartaoAsync(client);
        var parcelas = await ParcelarAsync(client, cardId, "Loja Parcelada Ficticia", 241.50m, 6, Compra);

        // A 4ª parcela está em agosto; a fatura de setembro a lista com a data de maio.
        await ImportarAsync(client, cardId, Item(Compra, "LOJA PARCELADA FICTICIA", 241.50m, "04/06"));

        var r = (await ConciliacoesAsync(client, cardId)).Single();
        Alvo(r, "financialTransactionId").Should().Be(parcelas[3]);
        r.GetProperty("status").GetString().Should().Be("Matched");
        r.GetProperty("reason").GetString().Should().Contain("Parcela 4/6");
    }

    [Fact]
    public async Task Rotulo_10_de_12_nao_deve_casar_com_a_parcela_1()
    {
        // Com Contains("1"), "10/12" casava com a parcela 1 — e com a 0, 2, … de qualquer plano.
        var client = await NovoUsuarioAsync();
        var cardId = await CriarCartaoAsync(client);
        var parcelas = await ParcelarAsync(client, cardId, "Loja Doze Vezes", 100m, 12, Compra.AddMonths(-9));

        await ImportarAsync(client, cardId, Item(Compra.AddMonths(-9), "LOJA DOZE VEZES", 100m, "10/12"));

        var r = (await ConciliacoesAsync(client, cardId)).Single();
        Alvo(r, "financialTransactionId").Should().Be(parcelas[9]);
    }

    [Fact]
    public async Task Com_varios_rotulos_na_fatura_cada_item_pega_o_numero_dele()
    {
        // A fatura traz "03/06" de outra compra: a parcela 3 deste plano também é candidata, com
        // data da 1ª parcela, valor e descrição iguais aos da 4 — só o número as separa.
        var client = await NovoUsuarioAsync();
        var cardId = await CriarCartaoAsync(client);
        var parcelas = await ParcelarAsync(client, cardId, "Loja Parcelada Ficticia", 241.50m, 6, Compra);

        await ImportarAsync(client, cardId,
            Item(Compra.AddMonths(-1), "OUTRA LOJA FICTICIA", 70m, "03/06"),
            Item(Compra, "LOJA PARCELADA FICTICIA", 241.50m, "04/06"));

        var r = (await ConciliacoesAsync(client, cardId)).Single(x => x.GetProperty("itemDescription").GetString() == "LOJA PARCELADA FICTICIA");
        Alvo(r, "financialTransactionId").Should().Be(parcelas[3]);
    }

    [Fact]
    public async Task Rotulo_de_outro_total_nao_deve_casar_com_o_plano()
    {
        // Parcela 4 de um plano de 10 não é a "04/06" da fatura.
        var client = await NovoUsuarioAsync();
        var cardId = await CriarCartaoAsync(client);
        await ParcelarAsync(client, cardId, "Loja Dez Vezes", 241.50m, 10, Compra);

        await ImportarAsync(client, cardId, Item(Compra, "LOJA DEZ VEZES", 241.50m, "04/06"));

        var r = (await ConciliacoesAsync(client, cardId)).Single();
        Alvo(r, "financialTransactionId").Should().BeNull();
        r.GetProperty("status").GetString().Should().Be("Unmatched");
    }

    [Fact]
    public async Task Duas_parcelas_iguais_de_planos_diferentes_devem_ir_para_lancamentos_diferentes()
    {
        var client = await NovoUsuarioAsync();
        var cardId = await CriarCartaoAsync(client);
        var a = await ParcelarAsync(client, cardId, "Loja Gemea", 50m, 3, Compra);
        var b = await ParcelarAsync(client, cardId, "Loja Gemea", 50m, 3, Compra);

        await ImportarAsync(client, cardId,
            Item(Compra, "LOJA GEMEA", 50m, "02/03"),
            Item(Compra, "LOJA GEMEA", 50m, "02/03"));

        var alvos = (await ConciliacoesAsync(client, cardId)).Select(r => Alvo(r, "financialTransactionId")).ToList();
        alvos.Should().BeEquivalentTo(new Guid?[] { a[1], b[1] });
    }

    [Fact]
    public async Task Compra_a_vista_continua_casando_pela_data_do_lancamento()
    {
        var client = await NovoUsuarioAsync();
        var cardId = await CriarCartaoAsync(client);
        var data = Fechamento.AddDays(-5);
        var compra = await GastarAsync(client, cardId, "Mercado Ficticio", 58.50m, data);

        await ImportarAsync(client, cardId, Item(data, "MERCADO FICTICIO", 58.50m));

        var r = (await ConciliacoesAsync(client, cardId)).Single();
        Alvo(r, "financialTransactionId").Should().Be(compra);
        r.GetProperty("status").GetString().Should().Be("Matched");
    }

    // ---------------- pagamento da fatura ----------------

    private static async Task<Guid> PagarAsync(HttpClient client, Guid cardId, Guid? contaId, DateOnly data, decimal valor) =>
        await CriarAsync(client, "/api/v1/card-payments", new
        {
            creditCardId = cardId,
            cycleReference = Iso(new DateOnly(Fechamento.Year, Fechamento.Month, 1).AddMonths(-1)),
            paymentDate = Iso(data),
            amount = valor,
            accountId = contaId,
        });

    [Fact]
    public async Task Linha_de_pagamento_deve_casar_com_o_pagamento_registrado_e_nao_com_lancamento()
    {
        var client = await NovoUsuarioAsync();
        var cardId = await CriarCartaoAsync(client);
        var contaId = await CriarContaAsync(client);
        var dataPagamento = new DateOnly(2026, 8, 10);
        var pagamento = await PagarAsync(client, cardId, contaId, dataPagamento, 1000m);

        await ImportarAsync(client, cardId, Item(dataPagamento, "PAGAMENTO DE FATURA-INTERNET", -1000m));

        var r = (await ConciliacoesAsync(client, cardId)).Single();
        Alvo(r, "cardPaymentId").Should().Be(pagamento);
        Alvo(r, "financialTransactionId").Should().BeNull();
        r.GetProperty("status").GetString().Should().Be("Matched");
        r.GetProperty("accepted").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Pagamento_aceito_nao_pode_ser_excluido_ate_desfazer()
    {
        var client = await NovoUsuarioAsync();
        var cardId = await CriarCartaoAsync(client);
        var contaId = await CriarContaAsync(client);
        var dataPagamento = new DateOnly(2026, 8, 10);
        var pagamento = await PagarAsync(client, cardId, contaId, dataPagamento, 1000m);
        await ImportarAsync(client, cardId, Item(dataPagamento, "PAGAMENTO DE FATURA", -1000m));
        var r = (await ConciliacoesAsync(client, cardId)).Single();
        var id = r.GetProperty("id").GetGuid();

        (await client.PostAsync($"/api/v1/reconciliations/card/{id}/accept", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await ConciliacoesAsync(client, cardId)).Single().GetProperty("accepted").GetBoolean().Should().BeTrue();

        // O lançamento do pagamento fica na conta e é conciliado com o extrato — não daqui.
        var lancamento = await JsonAsync(await client.GetAsync($"/api/v1/transactions?accountId={contaId}"));
        lancamento.GetProperty("items").EnumerateArray().Single().GetProperty("status").ToString().Should().NotMatchRegex("^(3|Reconciled)$");

        var excluir = await client.DeleteAsync($"/api/v1/card-payments/{pagamento}");
        excluir.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await excluir.Content.ReadAsStringAsync()).Should().Contain("pagamento-conciliado");

        (await client.PostAsync($"/api/v1/reconciliations/card/{id}/undo", null)).IsSuccessStatusCode.Should().BeTrue();
        (await client.DeleteAsync($"/api/v1/card-payments/{pagamento}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Excluir_pagamento_so_sugerido_deve_desfazer_a_sugestao()
    {
        var client = await NovoUsuarioAsync();
        var cardId = await CriarCartaoAsync(client);
        var dataPagamento = new DateOnly(2026, 8, 10);
        var pagamento = await PagarAsync(client, cardId, null, dataPagamento, 1000m);
        await ImportarAsync(client, cardId, Item(dataPagamento, "PAGAMENTO DE FATURA", -1000m));

        (await client.DeleteAsync($"/api/v1/card-payments/{pagamento}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var r = (await ConciliacoesAsync(client, cardId)).Single();
        Alvo(r, "cardPaymentId").Should().BeNull();
        r.GetProperty("status").GetString().Should().Be("Unmatched");
    }

    [Fact]
    public async Task Fatura_com_pagamento_aceito_nao_pode_ser_excluida()
    {
        var client = await NovoUsuarioAsync();
        var cardId = await CriarCartaoAsync(client);
        var dataPagamento = new DateOnly(2026, 8, 10);
        await PagarAsync(client, cardId, null, dataPagamento, 1000m);
        await ImportarAsync(client, cardId, Item(dataPagamento, "PAGAMENTO DE FATURA", -1000m));
        var id = (await ConciliacoesAsync(client, cardId)).Single().GetProperty("id").GetGuid();
        (await client.PostAsync($"/api/v1/reconciliations/card/{id}/accept", null)).StatusCode.Should().Be(HttpStatusCode.OK);

        var faturas = await JsonAsync(await client.GetAsync("/api/v1/card-invoices"));
        var faturaId = faturas.GetProperty("items").EnumerateArray().Single().GetProperty("id").GetGuid();

        var excluir = await client.DeleteAsync($"/api/v1/card-invoices/{faturaId}");
        excluir.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await excluir.Content.ReadAsStringAsync()).Should().Contain("fatura-com-conciliacao-aceita");
    }

    [Fact]
    public async Task Vincular_manualmente_pagamento_de_outro_grupo_deve_ser_proibido()
    {
        var dono = await NovoUsuarioAsync();
        var cartaoDono = await CriarCartaoAsync(dono);
        await ImportarAsync(dono, cartaoDono, Item(new DateOnly(2026, 8, 10), "PAGAMENTO DE FATURA", -1000m));
        var itemId = (await ConciliacoesAsync(dono, cartaoDono)).Single().GetProperty("cardInvoiceItemId").GetGuid();

        var outro = await NovoUsuarioAsync();
        var pagamentoAlheio = await PagarAsync(outro, await CriarCartaoAsync(outro), null, new DateOnly(2026, 8, 10), 1000m);

        var response = await dono.PostAsJsonAsync("/api/v1/reconciliations/card/manual",
            new { cardInvoiceItemId = itemId, cardPaymentId = pagamentoAlheio });
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Pagamento_de_outro_grupo_nao_deve_ser_sugerido()
    {
        var outro = await NovoUsuarioAsync();
        await PagarAsync(outro, await CriarCartaoAsync(outro), null, new DateOnly(2026, 8, 10), 1000m);

        var dono = await NovoUsuarioAsync();
        var cartao = await CriarCartaoAsync(dono);
        await ImportarAsync(dono, cartao, Item(new DateOnly(2026, 8, 10), "PAGAMENTO DE FATURA", -1000m));

        Alvo((await ConciliacoesAsync(dono, cartao)).Single(), "cardPaymentId").Should().BeNull();
    }
}
