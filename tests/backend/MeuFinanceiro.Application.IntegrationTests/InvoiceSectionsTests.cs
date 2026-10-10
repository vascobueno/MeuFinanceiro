using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using MeuFinanceiro.Application.Reconciliation.Import;
using MeuFinanceiro.Domain.Enums;
using Xunit;

namespace MeuFinanceiro.Application.IntegrationTests;

/// <summary>
/// Fatura lida por seções de cartão, com compra no exterior, cotação, IOF e total (docs/08).
/// Textos SINTÉTICOS: só a estrutura imita as faturas reais.
/// </summary>
public class InvoiceSectionsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public InvoiceSectionsTests(CustomWebApplicationFactory factory) => _factory = factory;

    private static readonly DateOnly Fechamento = new(2026, 9, 2);

    private const string Fatura = """
        PORTADOR FICTICIO - 5480 XXXX XXXX 1111
        Pagamento e Demais Créditos
        Compra Data Descrição Parcela R$ US$
        10/08 PAGAMENTO DE FATURA-INTERNET -1.000,00
        12/08 ESTORNO LOJA FICTICIA -10,00
        Parcelamentos
        1 19/05 LOJA PARCELADA 04/06 241,50
        Despesas
        3 06/08 LOJA FICTICIA A 58,50
        21/08 SERVICO NO EXTERIOR 116,65 21,22
        COTAÇÃO DOLAR R$ 5,4970
        IOF DESPESA NO EXTERIOR 4,08
        @ PORTADOR FICTICIO - 5480 XXXX XXXX 7310
        Despesas
        25/08 LOJA EUROPEIA EUR 19,50 116,65 21,22
        IOF DESPESA NO EXTERIOR 4,08
        VALOR TOTAL 531,38 42,44
        02/09 ANUIDADE DIFERENCIADA 08/12 0,00
        """;

    // ---------------- leitor ----------------

    [Fact]
    [Trait("Category", "Unit")]
    public void Cada_item_leva_o_final_do_cartao_da_secao()
    {
        var r = InvoiceTextReader.Read(Fatura, Fechamento);

        r.Errors.Should().BeEmpty();
        r.Cards.Should().HaveCount(2);
        r.Cards![0].Should().Be(new InvoiceCardHeader("1111", "5480", "PORTADOR FICTICIO", false));
        r.Cards[1].LastFour.Should().Be("7310");
        r.Cards[1].IsVirtual.Should().BeTrue();

        r.Items.Where(i => i.Description.StartsWith("LOJA EUROPEIA")).Single().CardLastFour.Should().Be("7310");
        r.Items.Single(i => i.Description == "LOJA FICTICIA A").CardLastFour.Should().Be("1111");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Classifica_pagamento_credito_parcela_tarifa_imposto_e_compra()
    {
        var r = InvoiceTextReader.Read(Fatura, Fechamento);
        CardInvoiceItemKind KindOf(string d) => r.Items.First(i => i.Description.StartsWith(d)).Kind!.Value;

        KindOf("PAGAMENTO DE FATURA").Should().Be(CardInvoiceItemKind.Payment);
        KindOf("ESTORNO").Should().Be(CardInvoiceItemKind.Credit);
        KindOf("LOJA PARCELADA").Should().Be(CardInvoiceItemKind.Installment);
        KindOf("LOJA FICTICIA A").Should().Be(CardInvoiceItemKind.Expense);
        KindOf("IOF").Should().Be(CardInvoiceItemKind.Tax);
        // Anuidade parcelada continua sendo tarifa, com o número da parcela guardado.
        var anuidade = r.Items.Single(i => i.Description.StartsWith("ANUIDADE"));
        anuidade.Kind.Should().Be(CardInvoiceItemKind.Fee);
        (anuidade.InstallmentNumber, anuidade.InstallmentTotal).Should().Be((8, 12));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Compra_no_exterior_guarda_valor_moeda_e_cotacao_e_o_IOF_aponta_para_ela()
    {
        var r = InvoiceTextReader.Read(Fatura, Fechamento);

        var compra = r.Items.Single(i => i.Description == "SERVICO NO EXTERIOR");
        compra.Amount.Should().Be(116.65m);
        compra.ForeignAmount.Should().Be(21.22m);
        compra.ForeignCurrency.Should().Be("USD");
        compra.ExchangeRate.Should().Be(5.4970m);
        compra.ExchangeRateEstimated.Should().BeFalse();

        var indice = r.Items.ToList().IndexOf(compra);
        var iof = r.Items[indice + 1];
        iof.Kind.Should().Be(CardInvoiceItemKind.Tax);
        iof.RelatedIndex.Should().Be(indice);
        iof.Date.Should().Be(compra.Date);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Sem_linha_de_cotacao_a_cotacao_e_calculada_e_marcada()
    {
        var r = InvoiceTextReader.Read(Fatura, Fechamento);
        var europeia = r.Items.Single(i => i.Description.StartsWith("LOJA EUROPEIA"));

        // 116,65 ÷ 21,22 = 5,4972 — a seção do virtual não trouxe COTAÇÃO.
        europeia.ExchangeRate.Should().Be(5.4972m);
        europeia.ExchangeRateEstimated.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Moeda_de_origem_na_descricao_e_guardada_e_nao_confundida_com_a_coluna()
    {
        var r = InvoiceTextReader.Read(Fatura, Fechamento);
        var europeia = r.Items.Single(i => i.Description.StartsWith("LOJA EUROPEIA"));
        europeia.Description.Should().Be("LOJA EUROPEIA EUR 19,50");
        (europeia.OriginalAmount, europeia.OriginalCurrency).Should().Be((19.50m, "EUR"));
        europeia.Amount.Should().Be(116.65m);

        // Sem coluna em dólar: o 19,50 depois de "EUR" é da descrição, não o valor em reais.
        var semColuna = InvoiceTextReader.Read("25/08 LOJA EUROPEIA EUR 19,50 116,65", Fechamento).Items.Single();
        semColuna.Amount.Should().Be(116.65m);
        semColuna.ForeignAmount.Should().BeNull();
        (semColuna.OriginalAmount, semColuna.OriginalCurrency).Should().Be((19.50m, "EUR"));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Le_o_valor_total_quando_ha_um_so()
    {
        InvoiceTextReader.Read(Fatura, Fechamento).TotalFromText.Should().Be(531.38m);
        InvoiceTextReader.Read("VALOR TOTAL 100,00\nVALOR TOTAL 200,00", Fechamento).TotalFromText.Should().BeNull();
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("PORTADOR FICTICIO - 5480 XXXX XXXX 1111", "1111", "5480", "PORTADOR FICTICIO", false)]
    [InlineData("@ PORTADOR FICTICIO - 4532 XXXX XXXX 7310", "7310", "4532", "PORTADOR FICTICIO", true)]
    [InlineData("Cartão final 4021", "4021", null, null, false)]
    [InlineData("PORTADORA FICTICIA **** 4022", "4022", null, "PORTADORA FICTICIA", false)]
    // Pan: o rótulo "Lançamentos do cartão adicional" e o "| Cartão" não fazem parte do nome.
    [InlineData("Lançamentos do cartão adicional Portadora Ficticia | Cartão final 4032", "4032", null, "Portadora Ficticia", false)]
    [InlineData("Portador Ficticio | Cartão titular final 4031", "4031", null, "Portador Ficticio", false)]
    public void Reconhece_cabecalhos_de_cartao(string linha, string final, string? inicio, string? nome, bool virtual_)
    {
        InvoiceTextReader.TryCardHeader(linha).Should().Be(new InvoiceCardHeader(final, inicio, nome, virtual_));
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("06/08 LOJA FICTICIA 1111 58,50")]
    [InlineData("Compra Data Descrição Parcela R$ US$")]
    [InlineData("Limite total 5.000,00")]
    public void Linha_de_compra_ou_de_resumo_nao_e_cabecalho(string linha)
    {
        InvoiceTextReader.TryCardHeader(linha).Should().BeNull();
    }

    // ---------------- serviço ----------------

    private async Task<HttpClient> NovoUsuarioAsync()
    {
        var id = Guid.NewGuid().ToString("N")[..12];
        var (client, _) = await AuthTestHelpers.RegisterConfirmAndLoginAsync(
            _factory, "Usuário Fatura", $"secoes{id}", $"secoes-{id}@teste.com", "SenhaForte!123");
        return client;
    }

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.Should().BeTrue(body);
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static async Task<Guid> GrupoComCartoesAsync(HttpClient client, params string[] finais)
    {
        var grupo = (await JsonAsync(await client.PostAsJsonAsync("/api/v1/cards",
            new { name = $"Grupo {Guid.NewGuid():N}"[..20], brand = "Mastercard", closingDay = 2, dueDay = 10 }))).GetProperty("id").GetGuid();
        foreach (var f in finais)
            await JsonAsync(await client.PostAsJsonAsync($"/api/v1/cards/{grupo}/issued-cards", new { kind = 1, lastFour = f }));
        return grupo;
    }

    private static async Task<JsonElement> LerAsync(HttpClient client, string texto) =>
        await JsonAsync(await client.PostAsJsonAsync("/api/v1/card-invoices/parse", new { text = texto, closingDate = "2026-09-02" }));

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Reconhece_o_grupo_pelos_finais_e_aponta_o_final_desconhecido()
    {
        var client = await NovoUsuarioAsync();
        var grupo = await GrupoComCartoesAsync(client, "1111");

        var lido = await LerAsync(client, Fatura);
        lido.GetProperty("detectedCreditCardId").GetGuid().Should().Be(grupo);
        lido.GetProperty("totalFromText").GetDecimal().Should().Be(531.38m);

        var cartoes = lido.GetProperty("cards").EnumerateArray().ToList();
        cartoes.Single(c => c.GetProperty("lastFour").GetString() == "1111").GetProperty("creditCardId").GetGuid().Should().Be(grupo);
        var desconhecido = cartoes.Single(c => c.GetProperty("lastFour").GetString() == "7310");
        desconhecido.GetProperty("issuedCardId").ValueKind.Should().Be(JsonValueKind.Null);
        desconhecido.GetProperty("isVirtual").GetBoolean().Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Finais_de_grupos_diferentes_nao_reconhecem_grupo_nenhum()
    {
        var client = await NovoUsuarioAsync();
        await GrupoComCartoesAsync(client, "1111");
        await GrupoComCartoesAsync(client, "7310");

        (await LerAsync(client, Fatura)).GetProperty("detectedCreditCardId").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Cartoes_de_outro_grupo_de_usuarios_nao_sao_reconhecidos()
    {
        var outro = await NovoUsuarioAsync();
        await GrupoComCartoesAsync(outro, "1111", "7310");

        var client = await NovoUsuarioAsync();
        var lido = await LerAsync(client, Fatura);
        lido.GetProperty("detectedCreditCardId").ValueKind.Should().Be(JsonValueKind.Null);
        lido.GetProperty("cards").EnumerateArray().Should().OnlyContain(c => c.GetProperty("issuedCardId").ValueKind == JsonValueKind.Null);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Importacao_guarda_cartao_tipo_cambio_e_a_ligacao_do_IOF()
    {
        var client = await NovoUsuarioAsync();
        var grupo = await GrupoComCartoesAsync(client, "1111");
        var lido = await LerAsync(client, Fatura);
        // O 7310 existe, mas em outro grupo do mesmo usuário: não pode ser ligado a esta fatura.
        await GrupoComCartoesAsync(client, "7310");

        await JsonAsync(await client.PostAsJsonAsync("/api/v1/card-invoices/import", new
        {
            creditCardId = grupo,
            closingDate = "2026-09-02",
            dueDate = "2026-09-10",
            totalAmount = lido.GetProperty("totalFromText").GetDecimal(),
            confirmUnbalanced = true,
            items = lido.GetProperty("items"),
        }));

        var itens = (await JsonAsync(await client.GetAsync($"/api/v1/reconciliations/card?creditCardId={grupo}"))).EnumerateArray().ToList();
        var compra = itens.Single(i => i.GetProperty("itemDescription").GetString() == "SERVICO NO EXTERIOR");
        compra.GetProperty("itemCardLastFour").GetString().Should().Be("1111");
        compra.GetProperty("itemIssuedCardId").ValueKind.Should().Be(JsonValueKind.String);
        compra.GetProperty("itemKind").GetString().Should().Be("Expense");
        compra.GetProperty("itemForeignAmount").GetDecimal().Should().Be(21.22m);
        compra.GetProperty("itemForeignCurrency").GetString().Should().Be("USD");
        compra.GetProperty("itemExchangeRate").GetDecimal().Should().Be(5.4970m);

        var iofs = itens.Where(i => i.GetProperty("itemKind").GetString() == "Tax").ToList();
        iofs.Should().HaveCount(2);
        iofs.Should().Contain(i => i.GetProperty("itemRelatedItemId").GetGuid() == compra.GetProperty("cardInvoiceItemId").GetGuid());

        var europeia = itens.Single(i => i.GetProperty("itemDescription").GetString()!.StartsWith("LOJA EUROPEIA"));
        // Final 7310 não está cadastrado: o item guarda o final, sem cartão ligado.
        europeia.GetProperty("itemCardLastFour").GetString().Should().Be("7310");
        europeia.GetProperty("itemIssuedCardId").ValueKind.Should().Be(JsonValueKind.Null);
        europeia.GetProperty("itemOriginalCurrency").GetString().Should().Be("EUR");
        europeia.GetProperty("itemExchangeRateEstimated").GetBoolean().Should().BeTrue();
    }
}
