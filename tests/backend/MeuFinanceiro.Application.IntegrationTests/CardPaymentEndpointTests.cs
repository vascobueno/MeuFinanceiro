using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using MeuFinanceiro.Application.Auth;
using Xunit;

namespace MeuFinanceiro.Application.IntegrationTests;

/// <summary>
/// Cobre a fatura calculada e o pagamento de cartão (docs/05 — Pagamento de cartão).
///
/// O teste que sustenta o desenho é
/// <see cref="Pagar_a_fatura_nao_pode_aumentar_as_despesas_do_mes"/>: a compra no cartão já é a
/// despesa, e se o pagamento entrasse como despesa na conta o mesmo dinheiro seria contado duas
/// vezes em todo relatório de gastos.
/// </summary>
[Trait("Category", "Integration")]
public class CardPaymentEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public CardPaymentEndpointTests(CustomWebApplicationFactory factory) => _factory = factory;

    // ---------------- apoio ----------------

    private async Task<HttpClient> NovoUsuarioAsync()
    {
        var id = Guid.NewGuid().ToString("N")[..12];
        var (client, _) = await AuthTestHelpers.RegisterConfirmAndLoginAsync(
            _factory, "Usuário Cartão", $"cartao{id}", $"cartao-{id}@teste.com", "SenhaForte!123");
        return client;
    }

    private static string Iso(DateOnly d) => d.ToString("yyyy-MM-dd");
    private static DateOnly Hoje => DateOnly.FromDateTime(DateTime.UtcNow);

    private static async Task<Guid> CriarContaAsync(HttpClient client, decimal saldoInicial)
    {
        var response = await client.PostAsJsonAsync("/api/v1/accounts", new
        {
            name = $"Conta {Guid.NewGuid():N}"[..20],
            bank = "Banco Teste",
            type = 1,
            initialBalance = saldoInicial,
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
    }

    /// <summary>Cartão com fechamento e vencimento escolhidos para o ciclo atual conter hoje.</summary>
    private static async Task<Guid> CriarCartaoAsync(HttpClient client, int closingDay = 28, int dueDay = 10)
    {
        var response = await client.PostAsJsonAsync("/api/v1/cards", new
        {
            name = $"Cartao {Guid.NewGuid():N}"[..20],
            brand = "Visa",
            closingDay,
            dueDay,
            limit = 10000m,
        });
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.OK);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
    }

    private static async Task GastarNoCartaoAsync(HttpClient client, Guid cardId, decimal valor, DateOnly? data = null, int direction = 1)
    {
        var response = await client.PostAsJsonAsync("/api/v1/transactions", new
        {
            transactionDate = Iso(data ?? Hoje),
            description = $"Compra {Guid.NewGuid():N}"[..20],
            creditCardId = cardId,
            type = 2,
            direction,
            amount = valor,
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static async Task<JsonElement> FaturaAsync(HttpClient client, Guid cardId, DateOnly? reference = null)
    {
        var url = reference is null
            ? $"/api/v1/cards/{cardId}/invoice"
            : $"/api/v1/cards/{cardId}/invoice?reference={Iso(reference.Value)}";

        var response = await client.GetAsync(url);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
    }

    private static async Task<HttpResponseMessage> PagarAsync(
        HttpClient client, Guid cardId, DateOnly referencia, decimal valor, Guid? accountId) =>
        await client.PostAsJsonAsync("/api/v1/card-payments", new
        {
            creditCardId = cardId,
            cycleReference = Iso(referencia),
            paymentDate = Iso(Hoje),
            amount = valor,
            accountId,
        });

    private static async Task<decimal> SaldoAsync(HttpClient client, Guid accountId)
    {
        var response = await client.GetAsync($"/api/v1/accounts/{accountId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync())
            .RootElement.GetProperty("currentBalance").GetDecimal();
    }

    /// <summary>Soma das despesas (Type = Expense) do período — o oráculo da contagem dupla.</summary>
    private static async Task<decimal> DespesasDoPeriodoAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/v1/transactions?page=1&pageSize=100&type=2");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        return JsonDocument.Parse(await response.Content.ReadAsStringAsync())
            .RootElement.GetProperty("items").EnumerateArray()
            .Sum(i => i.GetProperty("amount").GetDecimal());
    }

    // ---------------- a fatura ----------------

    [Fact]
    public async Task Fatura_deve_somar_os_gastos_do_ciclo()
    {
        var client = await NovoUsuarioAsync();
        var cardId = await CriarCartaoAsync(client);

        await GastarNoCartaoAsync(client, cardId, 300m);
        await GastarNoCartaoAsync(client, cardId, 200m);

        var fatura = await FaturaAsync(client, cardId);

        fatura.GetProperty("total").GetDecimal().Should().Be(500m);
        fatura.GetProperty("paid").GetDecimal().Should().Be(0m);
        fatura.GetProperty("outstanding").GetDecimal().Should().Be(500m);
    }

    [Fact]
    public async Task Fatura_deve_incluir_lancamento_pendente()
    {
        // Contraintuitivo de propósito: o saldo de conta EXCLUI pendentes, mas os lançamentos
        // nascem Pending — se a fatura seguisse a mesma regra, nasceria zerada para todo mundo.
        var client = await NovoUsuarioAsync();
        var cardId = await CriarCartaoAsync(client);

        await GastarNoCartaoAsync(client, cardId, 150m);

        var lista = await client.GetAsync($"/api/v1/transactions?page=1&pageSize=10&creditCardId={cardId}");
        var status = JsonDocument.Parse(await lista.Content.ReadAsStringAsync())
            .RootElement.GetProperty("items")[0].GetProperty("status").GetInt32();
        status.Should().Be(1, "o lançamento tem de nascer Pending para o teste valer");

        (await FaturaAsync(client, cardId)).GetProperty("total").GetDecimal().Should().Be(150m);
    }

    [Fact]
    public async Task Estorno_no_cartao_deve_reduzir_a_fatura()
    {
        var client = await NovoUsuarioAsync();
        var cardId = await CriarCartaoAsync(client);

        await GastarNoCartaoAsync(client, cardId, 400m);
        await GastarNoCartaoAsync(client, cardId, 100m, direction: 2); // Credit = estorno

        (await FaturaAsync(client, cardId)).GetProperty("total").GetDecimal().Should().Be(300m);
    }

    [Fact]
    public async Task Referencia_que_nao_e_o_primeiro_dia_do_mes_deve_ser_recusada()
    {
        var client = await NovoUsuarioAsync();
        var cardId = await CriarCartaoAsync(client, closingDay: 28);

        // A referência é o MÊS (dia 1); qualquer outro dia é recusado.
        var response = await client.GetAsync(
            $"/api/v1/cards/{cardId}/invoice?reference={Iso(new DateOnly(Hoje.Year, Hoje.Month, 15))}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ---------------- o pagamento ----------------

    [Fact]
    public async Task Pagar_de_uma_conta_deve_derrubar_o_saldo_e_a_fatura()
    {
        var client = await NovoUsuarioAsync();
        var contaId = await CriarContaAsync(client, 5000m);
        var cardId = await CriarCartaoAsync(client);
        await GastarNoCartaoAsync(client, cardId, 800m);

        var fatura = await FaturaAsync(client, cardId);
        var referencia = fatura.GetProperty("reference").GetDateTime();

        var pagamento = await PagarAsync(client, cardId, DateOnly.FromDateTime(referencia), 800m, contaId);
        pagamento.StatusCode.Should().Be(HttpStatusCode.OK);

        (await SaldoAsync(client, contaId)).Should().Be(4200m);

        var depois = await FaturaAsync(client, cardId);
        depois.GetProperty("paid").GetDecimal().Should().Be(800m);
        depois.GetProperty("outstanding").GetDecimal().Should().Be(0m);
        depois.GetProperty("payments").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task Pagar_a_fatura_nao_pode_aumentar_as_despesas_do_mes()
    {
        // A trava contra a contagem dupla: a compra no cartão já é a despesa.
        var client = await NovoUsuarioAsync();
        var contaId = await CriarContaAsync(client, 5000m);
        var cardId = await CriarCartaoAsync(client);
        await GastarNoCartaoAsync(client, cardId, 600m);

        var despesasAntes = await DespesasDoPeriodoAsync(client);
        despesasAntes.Should().Be(600m);

        var referencia = DateOnly.FromDateTime((await FaturaAsync(client, cardId)).GetProperty("reference").GetDateTime());
        (await PagarAsync(client, cardId, referencia, 600m, contaId)).StatusCode.Should().Be(HttpStatusCode.OK);

        (await DespesasDoPeriodoAsync(client)).Should().Be(despesasAntes,
            "o pagamento é movimentação, não despesa — contá-lo duplicaria o mesmo dinheiro");
        (await SaldoAsync(client, contaId)).Should().Be(4400m, "mas o dinheiro saiu mesmo da conta");
    }

    [Fact]
    public async Task Pagar_por_outra_forma_deve_baixar_a_fatura_sem_tocar_em_conta()
    {
        var client = await NovoUsuarioAsync();
        var contaId = await CriarContaAsync(client, 5000m);
        var cardId = await CriarCartaoAsync(client);
        await GastarNoCartaoAsync(client, cardId, 250m);

        var referencia = DateOnly.FromDateTime((await FaturaAsync(client, cardId)).GetProperty("reference").GetDateTime());
        (await PagarAsync(client, cardId, referencia, 250m, accountId: null)).StatusCode.Should().Be(HttpStatusCode.OK);

        (await FaturaAsync(client, cardId)).GetProperty("outstanding").GetDecimal().Should().Be(0m);
        (await SaldoAsync(client, contaId)).Should().Be(5000m, "nenhuma conta pode ser afetada");
    }

    [Fact]
    public async Task Pagamento_parcial_deve_deixar_o_restante_em_aberto()
    {
        var client = await NovoUsuarioAsync();
        var contaId = await CriarContaAsync(client, 5000m);
        var cardId = await CriarCartaoAsync(client);
        await GastarNoCartaoAsync(client, cardId, 1000m);

        var referencia = DateOnly.FromDateTime((await FaturaAsync(client, cardId)).GetProperty("reference").GetDateTime());
        await PagarAsync(client, cardId, referencia, 300m, contaId);

        var fatura = await FaturaAsync(client, cardId);
        fatura.GetProperty("paid").GetDecimal().Should().Be(300m);
        fatura.GetProperty("outstanding").GetDecimal().Should().Be(700m);
    }

    [Fact]
    public async Task Valor_zero_ou_negativo_deve_ser_recusado()
    {
        var client = await NovoUsuarioAsync();
        var cardId = await CriarCartaoAsync(client);
        var referencia = DateOnly.FromDateTime((await FaturaAsync(client, cardId)).GetProperty("reference").GetDateTime());

        (await PagarAsync(client, cardId, referencia, 0m, null)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await PagarAsync(client, cardId, referencia, -50m, null)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ---------------- trocar a conta ----------------

    [Fact]
    public async Task Trocar_a_conta_do_pagamento_deve_mover_o_debito()
    {
        var client = await NovoUsuarioAsync();
        var contaA = await CriarContaAsync(client, 3000m);
        var contaB = await CriarContaAsync(client, 3000m);
        var cardId = await CriarCartaoAsync(client);
        await GastarNoCartaoAsync(client, cardId, 500m);

        var referencia = DateOnly.FromDateTime((await FaturaAsync(client, cardId)).GetProperty("reference").GetDateTime());
        var criado = await PagarAsync(client, cardId, referencia, 500m, contaA);
        var pagamentoId = JsonDocument.Parse(await criado.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();

        (await SaldoAsync(client, contaA)).Should().Be(2500m);
        (await SaldoAsync(client, contaB)).Should().Be(3000m);

        var troca = await client.PutAsJsonAsync($"/api/v1/card-payments/{pagamentoId}", new
        {
            paymentDate = Iso(Hoje),
            amount = 500m,
            accountId = contaB,
        });
        troca.StatusCode.Should().Be(HttpStatusCode.OK);

        (await SaldoAsync(client, contaA)).Should().Be(3000m, "a conta antiga tem de voltar ao valor anterior");
        (await SaldoAsync(client, contaB)).Should().Be(2500m, "e a nova tem de cair");
        (await FaturaAsync(client, cardId)).GetProperty("outstanding").GetDecimal().Should().Be(0m);
    }

    [Fact]
    public async Task Trocar_de_conta_para_outra_forma_e_voltar_deve_funcionar_nos_dois_sentidos()
    {
        var client = await NovoUsuarioAsync();
        var contaId = await CriarContaAsync(client, 2000m);
        var cardId = await CriarCartaoAsync(client);
        await GastarNoCartaoAsync(client, cardId, 400m);

        var referencia = DateOnly.FromDateTime((await FaturaAsync(client, cardId)).GetProperty("reference").GetDateTime());
        var criado = await PagarAsync(client, cardId, referencia, 400m, contaId);
        var pagamentoId = JsonDocument.Parse(await criado.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
        (await SaldoAsync(client, contaId)).Should().Be(1600m);

        // conta -> outra forma: o débito some, a fatura segue paga.
        (await client.PutAsJsonAsync($"/api/v1/card-payments/{pagamentoId}",
            new { paymentDate = Iso(Hoje), amount = 400m, accountId = (Guid?)null })).StatusCode.Should().Be(HttpStatusCode.OK);

        (await SaldoAsync(client, contaId)).Should().Be(2000m);
        (await FaturaAsync(client, cardId)).GetProperty("outstanding").GetDecimal().Should().Be(0m);

        // outra forma -> conta: o débito volta.
        (await client.PutAsJsonAsync($"/api/v1/card-payments/{pagamentoId}",
            new { paymentDate = Iso(Hoje), amount = 400m, accountId = contaId })).StatusCode.Should().Be(HttpStatusCode.OK);

        (await SaldoAsync(client, contaId)).Should().Be(1600m);
    }

    [Fact]
    public async Task Excluir_o_pagamento_deve_desfazer_os_dois_lados()
    {
        var client = await NovoUsuarioAsync();
        var contaId = await CriarContaAsync(client, 1000m);
        var cardId = await CriarCartaoAsync(client);
        await GastarNoCartaoAsync(client, cardId, 200m);

        var referencia = DateOnly.FromDateTime((await FaturaAsync(client, cardId)).GetProperty("reference").GetDateTime());
        var criado = await PagarAsync(client, cardId, referencia, 200m, contaId);
        var pagamentoId = JsonDocument.Parse(await criado.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();

        (await client.DeleteAsync($"/api/v1/card-payments/{pagamentoId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await SaldoAsync(client, contaId)).Should().Be(1000m);
        var fatura = await FaturaAsync(client, cardId);
        fatura.GetProperty("outstanding").GetDecimal().Should().Be(200m);
        fatura.GetProperty("payments").GetArrayLength().Should().Be(0);
    }

    // ---------------- isolamento e filtro ----------------

    [Fact]
    public async Task Cartao_de_outro_grupo_nao_deve_ser_consultado()
    {
        var clientA = await NovoUsuarioAsync();
        var cardId = await CriarCartaoAsync(clientA);

        var clientB = await NovoUsuarioAsync();

        (await clientB.GetAsync($"/api/v1/cards/{cardId}/invoice")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Filtro_de_origem_deve_separar_conta_de_cartao()
    {
        var client = await NovoUsuarioAsync();
        var contaId = await CriarContaAsync(client, 1000m);
        var cardId = await CriarCartaoAsync(client);

        await GastarNoCartaoAsync(client, cardId, 100m);
        (await client.PostAsJsonAsync("/api/v1/transactions", new
        {
            transactionDate = Iso(Hoje),
            description = "Compra na conta",
            accountId = contaId,
            type = 2,
            direction = 1,
            amount = 50m,
        })).StatusCode.Should().Be(HttpStatusCode.OK);

        var deConta = await client.GetAsync("/api/v1/transactions?page=1&pageSize=50&origin=1");
        var deCartao = await client.GetAsync("/api/v1/transactions?page=1&pageSize=50&origin=2");

        JsonDocument.Parse(await deConta.Content.ReadAsStringAsync()).RootElement.GetProperty("items")
            .EnumerateArray().Should().OnlyContain(i => i.GetProperty("accountId").ValueKind != JsonValueKind.Null);
        JsonDocument.Parse(await deCartao.Content.ReadAsStringAsync()).RootElement.GetProperty("items")
            .EnumerateArray().Should().OnlyContain(i => i.GetProperty("creditCardId").ValueKind != JsonValueKind.Null);
    }

    // ---------------- ajuste do fechamento por fatura ----------------

    private static Task<HttpResponseMessage> AjustarFechamentoAsync(
        HttpClient client, Guid cardId, DateOnly referencia, DateOnly fechamento) =>
        client.PutAsJsonAsync($"/api/v1/cards/{cardId}/invoice/{Iso(referencia)}/closing",
            new { closingDate = Iso(fechamento) });

    [Fact]
    public async Task Ajustar_o_fechamento_deve_mudar_a_janela_do_ciclo()
    {
        var client = await NovoUsuarioAsync();
        var cardId = await CriarCartaoAsync(client, closingDay: 28, dueDay: 10);

        var referencia = DateOnly.FromDateTime((await FaturaAsync(client, cardId)).GetProperty("reference").GetDateTime());
        var original = DateOnly.FromDateTime((await FaturaAsync(client, cardId)).GetProperty("closingDate").GetDateTime());
        original.Day.Should().Be(28);

        var antecipado = new DateOnly(referencia.Year, referencia.Month, 20);
        (await AjustarFechamentoAsync(client, cardId, referencia, antecipado)).StatusCode.Should().Be(HttpStatusCode.OK);

        var fatura = await FaturaAsync(client, cardId, referencia);
        DateOnly.FromDateTime(fatura.GetProperty("closingDate").GetDateTime()).Should().Be(antecipado);
        fatura.GetProperty("isClosingOverridden").GetBoolean().Should().BeTrue();
        DateOnly.FromDateTime(fatura.GetProperty("defaultClosingDate").GetDateTime()).Should().Be(original);
    }

    [Fact]
    public async Task Gasto_depois_do_fechamento_ajustado_deve_sair_do_ciclo()
    {
        var client = await NovoUsuarioAsync();
        var cardId = await CriarCartaoAsync(client, closingDay: 28, dueDay: 10);
        var referencia = DateOnly.FromDateTime((await FaturaAsync(client, cardId)).GetProperty("reference").GetDateTime());

        // Gasto no dia 25: dentro do ciclo enquanto o fechamento é 28.
        await GastarNoCartaoAsync(client, cardId, 400m, new DateOnly(referencia.Year, referencia.Month, 25));
        (await FaturaAsync(client, cardId, referencia)).GetProperty("total").GetDecimal().Should().Be(400m);

        // Antecipando o fechamento para o dia 20, ele passa a pertencer ao ciclo seguinte.
        await AjustarFechamentoAsync(client, cardId, referencia, new DateOnly(referencia.Year, referencia.Month, 20));

        (await FaturaAsync(client, cardId, referencia)).GetProperty("total").GetDecimal().Should().Be(0m);
        (await FaturaAsync(client, cardId, referencia.AddMonths(1))).GetProperty("total").GetDecimal().Should().Be(400m);
    }

    [Fact]
    public async Task Pagamento_deve_continuar_na_fatura_depois_do_ajuste()
    {
        // É a razão de a chave do ciclo ser o MÊS: se fosse a data de fechamento, este pagamento
        // ficaria órfão no ajuste e sumiria da tela sem aviso.
        var client = await NovoUsuarioAsync();
        var contaId = await CriarContaAsync(client, 5000m);
        var cardId = await CriarCartaoAsync(client, closingDay: 28, dueDay: 10);
        var referencia = DateOnly.FromDateTime((await FaturaAsync(client, cardId)).GetProperty("reference").GetDateTime());

        await GastarNoCartaoAsync(client, cardId, 300m, new DateOnly(referencia.Year, referencia.Month, 10));
        (await PagarAsync(client, cardId, referencia, 300m, contaId)).StatusCode.Should().Be(HttpStatusCode.OK);

        await AjustarFechamentoAsync(client, cardId, referencia, new DateOnly(referencia.Year, referencia.Month, 20));

        var fatura = await FaturaAsync(client, cardId, referencia);
        fatura.GetProperty("payments").GetArrayLength().Should().Be(1, "o pagamento é do mês, não do fechamento");
        fatura.GetProperty("paid").GetDecimal().Should().Be(300m);
    }

    [Fact]
    public async Task Vencimento_NAO_deve_acompanhar_o_fechamento_ajustado()
    {
        // MUDANÇA DE COMPORTAMENTO: antes o vencimento era derivado do fechamento EFETIVO, então
        // antecipar o fechamento o puxava junto — às vezes um mês inteiro. A operadora antecipa o
        // fechamento, mas o dia de vencimento costuma ser fixo. Agora ele sai do fechamento PADRÃO.
        var client = await NovoUsuarioAsync();
        var cardId = await CriarCartaoAsync(client, closingDay: 28, dueDay: 10);
        var referencia = DateOnly.FromDateTime((await FaturaAsync(client, cardId)).GetProperty("reference").GetDateTime());

        var vencimentoAntes = DateOnly.FromDateTime(
            (await FaturaAsync(client, cardId, referencia)).GetProperty("dueDate").GetDateTime());
        vencimentoAntes.Should().Be(new DateOnly(referencia.Year, referencia.Month, 10).AddMonths(1));

        // Antecipa o fechamento para bem antes do dia 10: na regra antiga o vencimento viria para
        // o dia 10 DESTE mês; na nova, não se move.
        await AjustarFechamentoAsync(client, cardId, referencia, new DateOnly(referencia.Year, referencia.Month, 5));

        var vencimentoDepois = DateOnly.FromDateTime(
            (await FaturaAsync(client, cardId, referencia)).GetProperty("dueDate").GetDateTime());
        vencimentoDepois.Should().Be(vencimentoAntes, "o vencimento não acompanha o fechamento ajustado");
    }

    [Fact]
    public async Task Vencimento_deve_ser_empurrado_quando_o_fechamento_passa_dele()
    {
        // O piso: uma fatura não pode vencer antes de fechar. Só aqui o vencimento se move.
        var client = await NovoUsuarioAsync();
        var cardId = await CriarCartaoAsync(client, closingDay: 20, dueDay: 25);
        var referencia = DateOnly.FromDateTime((await FaturaAsync(client, cardId)).GetProperty("reference").GetDateTime());

        // Padrão: fecha 20, vence 25 do mesmo mês.
        DateOnly.FromDateTime((await FaturaAsync(client, cardId, referencia)).GetProperty("dueDate").GetDateTime())
            .Should().Be(new DateOnly(referencia.Year, referencia.Month, 25));

        // Adiando o fechamento para 27, o vencimento 25 ficaria no passado — tem de ir para o 25
        // do mês seguinte.
        await AjustarFechamentoAsync(client, cardId, referencia, new DateOnly(referencia.Year, referencia.Month, 27));

        DateOnly.FromDateTime((await FaturaAsync(client, cardId, referencia)).GetProperty("dueDate").GetDateTime())
            .Should().Be(new DateOnly(referencia.Year, referencia.Month, 25).AddMonths(1));
    }

    [Fact]
    public async Task Fechamento_fora_do_mes_de_referencia_deve_ser_recusado()
    {
        // Sem este limite, dois ciclos se sobreporiam (lançamento contado duas vezes) ou se
        // inverteriam (janela vazia) — os dois erram em silêncio.
        var client = await NovoUsuarioAsync();
        var cardId = await CriarCartaoAsync(client);
        var referencia = DateOnly.FromDateTime((await FaturaAsync(client, cardId)).GetProperty("reference").GetDateTime());

        (await AjustarFechamentoAsync(client, cardId, referencia, referencia.AddMonths(1)))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await AjustarFechamentoAsync(client, cardId, referencia, referencia.AddDays(-1)))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Remover_o_ajuste_deve_voltar_ao_fechamento_do_cadastro()
    {
        var client = await NovoUsuarioAsync();
        var cardId = await CriarCartaoAsync(client, closingDay: 28, dueDay: 10);
        var referencia = DateOnly.FromDateTime((await FaturaAsync(client, cardId)).GetProperty("reference").GetDateTime());

        await AjustarFechamentoAsync(client, cardId, referencia, new DateOnly(referencia.Year, referencia.Month, 20));

        var remocao = await client.DeleteAsync($"/api/v1/cards/{cardId}/invoice/{Iso(referencia)}/closing");
        remocao.StatusCode.Should().Be(HttpStatusCode.OK);

        var fatura = await FaturaAsync(client, cardId, referencia);
        DateOnly.FromDateTime(fatura.GetProperty("closingDate").GetDateTime()).Day.Should().Be(28);
        fatura.GetProperty("isClosingOverridden").GetBoolean().Should().BeFalse();
    }
}
