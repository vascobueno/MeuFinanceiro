using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using MeuFinanceiro.Application.Auth;
using MeuFinanceiro.Application.Reconciliation;
using Xunit;

namespace MeuFinanceiro.Application.IntegrationTests;

/// <summary>
/// Defeitos de importação encontrados com extratos e faturas reais:
/// valor/data no formato do banco recusados, o mesmo documento entrando duas vezes, extrato ou
/// fatura que não fecha entrando calado, e nenhum jeito de excluir uma importação errada.
/// </summary>
[Trait("Category", "Integration")]
public class ImportIntegrityEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public ImportIntegrityEndpointTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _factory = factory;
    }

    private async Task AuthenticateAsync()
    {
        var id = Guid.NewGuid().ToString("N")[..12];
        var email = $"user-{id}@teste.com";
        var username = $"user{id}";

        await _client.PostAsJsonAsync("/api/v1/auth/register",
            new RegisterRequest("Usuário Teste", username, email, "SenhaForte!123"));
        await AuthTestHelpers.ConfirmEmailAsync(_client, _factory.EmailSender, email);
        await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(username, "SenhaForte!123"));
        await AuthTestHelpers.AttachCsrfTokenAsync(_client);
    }

    private async Task<Guid> CreateAsync(string path, object body)
    {
        var response = await _client.PostAsJsonAsync(path, body);
        response.IsSuccessStatusCode.Should().BeTrue($"criar em {path} devolveu {(int)response.StatusCode}");
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("id").GetGuid();
    }

    private Task<Guid> CreateAccountAsync() => CreateAsync("/api/v1/accounts",
        new { name = "Conta Teste", bank = "Banco Teste", type = 1, initialBalance = 0m });

    private Task<Guid> CreateCardAsync() => CreateAsync("/api/v1/cards",
        new { name = "Cartão Teste", brand = "Visa", closingDay = 2, dueDay = 10, limit = 5000m });

    private static ImportBankStatementRequest Statement(Guid accountId, decimal opening, decimal closing, bool confirm = false,
        params (string date, string description, decimal amount)[] items) =>
        new(accountId, new DateOnly(2023, 1, 1), new DateOnly(2023, 1, 31), opening, closing, "extrato.pdf",
            items.Select(i => new BankStatementItemRequest(DateOnly.Parse(i.date), i.description, null,
                i.amount < 0 ? "OTHER_EXPENSE" : "OTHER_INCOME", i.amount)).ToList(),
            confirm);

    private static async Task ShouldBeRuleAsync(HttpResponseMessage response, string errorType, string? messagePart = null)
    {
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("type").GetString().Should().EndWith("/" + errorType);
        if (messagePart is not null) problem.GetProperty("title").GetString().Should().Contain(messagePart);
    }

    private async Task<JsonElement> ListAsync(string path)
    {
        var json = await _client.GetFromJsonAsync<JsonElement>(path);
        return json.GetProperty("items");
    }

    // ------------------------------------------------------------------ leitura do texto colado

    [Fact]
    public async Task Leitura_do_extrato_devolve_movimentos_e_saldos_do_texto_colado()
    {
        await AuthenticateAsync();

        var response = await _client.PostAsJsonAsync("/api/v1/bank-statements/parse", new
        {
            text = "SALDO EM 31/12 100,00\n02/01 COMPRA CARTAO DEB MC 200652 18,00-\n02/01 LOJA FICTICIA\nSALDO EM 31/01 82,00",
            referenceStart = "2023-01-01",
            referenceEnd = "2023-01-31"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var parsed = await response.Content.ReadFromJsonAsync<JsonElement>();
        parsed.GetProperty("items").GetArrayLength().Should().Be(1);
        parsed.GetProperty("items")[0].GetProperty("amount").GetDecimal().Should().Be(-18m);
        parsed.GetProperty("openingBalance").GetDecimal().Should().Be(100m);
        parsed.GetProperty("closingBalance").GetDecimal().Should().Be(82m);
        parsed.GetProperty("errors").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Leitura_da_fatura_devolve_itens_com_parcela()
    {
        await AuthenticateAsync();

        var response = await _client.PostAsJsonAsync("/api/v1/card-invoices/parse", new
        {
            text = "1 19/05 LOJA PARCELADA 04/06 788,52\nVALOR TOTAL 788,52 0,00",
            closingDate = "2026-09-02"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var parsed = await response.Content.ReadFromJsonAsync<JsonElement>();
        parsed.GetProperty("items").GetArrayLength().Should().Be(1);
        parsed.GetProperty("items")[0].GetProperty("installmentLabel").GetString().Should().Be("04/06");
    }

    // ------------------------------------------------------------------ extrato que não fecha

    [Fact]
    public async Task Extrato_que_nao_fecha_exige_confirmacao_e_fica_marcado()
    {
        await AuthenticateAsync();
        var accountId = await CreateAccountAsync();
        var items = new[] { ("2023-01-02", "COMPRA", -18m) };

        var semConfirmar = await _client.PostAsJsonAsync("/api/v1/bank-statements/import", Statement(accountId, 100m, 50m, false, items));
        await ShouldBeRuleAsync(semConfirmar, "extrato-nao-fecha", "diferença");

        var confirmado = await _client.PostAsJsonAsync("/api/v1/bank-statements/import", Statement(accountId, 100m, 50m, true, items));
        confirmado.StatusCode.Should().Be(HttpStatusCode.OK);

        var lista = await ListAsync("/api/v1/bank-statements");
        lista[0].GetProperty("balanceValidated").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Extrato_que_fecha_entra_sem_confirmacao_e_fica_marcado_como_conferido()
    {
        await AuthenticateAsync();
        var accountId = await CreateAccountAsync();

        var response = await _client.PostAsJsonAsync("/api/v1/bank-statements/import",
            Statement(accountId, 100m, 82m, false, ("2023-01-02", "COMPRA", -18m)));
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var lista = await ListAsync("/api/v1/bank-statements");
        lista[0].GetProperty("balanceValidated").GetBoolean().Should().BeTrue();
    }

    // ------------------------------------------------------------------ duplicidade

    [Fact]
    public async Task Mesmo_extrato_nao_entra_duas_vezes()
    {
        await AuthenticateAsync();
        var accountId = await CreateAccountAsync();
        var extrato = Statement(accountId, 100m, 82m, false, ("2023-01-02", "COMPRA", -18m));

        (await _client.PostAsJsonAsync("/api/v1/bank-statements/import", extrato)).StatusCode.Should().Be(HttpStatusCode.OK);

        var segunda = await _client.PostAsJsonAsync("/api/v1/bank-statements/import", extrato);
        await ShouldBeRuleAsync(segunda, "extrato-duplicado", "já foi importado");

        (await ListAsync("/api/v1/bank-statements")).GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task Mesma_fatura_nao_entra_duas_vezes()
    {
        await AuthenticateAsync();
        var cardId = await CreateCardAsync();
        var fatura = new ImportCardInvoiceRequest(cardId, new DateOnly(2026, 9, 2), new DateOnly(2026, 9, 10), 320m, "fatura.pdf",
            [new CardInvoiceItemRequest(new DateOnly(2026, 8, 20), "MERCADO", null, 320m)]);

        (await _client.PostAsJsonAsync("/api/v1/card-invoices/import", fatura)).StatusCode.Should().Be(HttpStatusCode.OK);

        var segunda = await _client.PostAsJsonAsync("/api/v1/card-invoices/import", fatura);
        await ShouldBeRuleAsync(segunda, "fatura-duplicada", "já foi importada");

        (await ListAsync($"/api/v1/card-invoices?creditCardId={cardId}")).GetArrayLength().Should().Be(1);
    }

    // ------------------------------------------------------------------ fatura que não fecha

    [Fact]
    public async Task Fatura_fecha_com_o_saldo_anterior_e_o_pagamento_negativo()
    {
        await AuthenticateAsync();
        var cardId = await CreateCardAsync();
        // Como numa fatura real: saldo anterior 1.000, pagamento dele (-1.000) e compras de 320.
        var itens = new List<CardInvoiceItemRequest>
        {
            new(new DateOnly(2026, 8, 10), "PAGAMENTO DE FATURA", null, -1000m),
            new(new DateOnly(2026, 8, 20), "MERCADO", null, 320m)
        };

        var semSaldoAnterior = await _client.PostAsJsonAsync("/api/v1/card-invoices/import",
            new ImportCardInvoiceRequest(cardId, new DateOnly(2026, 9, 2), new DateOnly(2026, 9, 10), 320m, null, itens));
        await ShouldBeRuleAsync(semSaldoAnterior, "fatura-nao-fecha", "diferença");

        var comSaldoAnterior = await _client.PostAsJsonAsync("/api/v1/card-invoices/import",
            new ImportCardInvoiceRequest(cardId, new DateOnly(2026, 9, 2), new DateOnly(2026, 9, 10), 320m, null, itens, PreviousBalance: 1000m));
        comSaldoAnterior.StatusCode.Should().Be(HttpStatusCode.OK);

        var lista = await ListAsync($"/api/v1/card-invoices?creditCardId={cardId}");
        lista[0].GetProperty("balanceValidated").GetBoolean().Should().BeTrue();
    }

    // ------------------------------------------------------------------ exclusão

    [Fact]
    public async Task Excluir_extrato_sem_conciliacao_aceita_libera_a_reimportacao()
    {
        await AuthenticateAsync();
        var accountId = await CreateAccountAsync();
        var extrato = Statement(accountId, 100m, 82m, false, ("2023-01-02", "COMPRA", -18m));
        (await _client.PostAsJsonAsync("/api/v1/bank-statements/import", extrato)).StatusCode.Should().Be(HttpStatusCode.OK);
        var id = (await ListAsync("/api/v1/bank-statements"))[0].GetProperty("id").GetGuid();

        (await _client.DeleteAsync($"/api/v1/bank-statements/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ListAsync("/api/v1/bank-statements")).GetArrayLength().Should().Be(0);

        // A impressão digital saiu junto: importar de novo, agora corrigido, funciona.
        (await _client.PostAsJsonAsync("/api/v1/bank-statements/import", extrato)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Excluir_extrato_com_conciliacao_aceita_e_bloqueado()
    {
        await AuthenticateAsync();
        var accountId = await CreateAccountAsync();
        // POST /transactions devolve uma lista (parcelamento gera várias): só confere o sucesso.
        (await _client.PostAsJsonAsync("/api/v1/transactions", new
        {
            transactionDate = "2023-01-02", description = "COMPRA LOJA FICTICIA",
            accountId, type = 2, direction = -1, amount = 18.00m
        })).IsSuccessStatusCode.Should().BeTrue();
        (await _client.PostAsJsonAsync("/api/v1/bank-statements/import",
            Statement(accountId, 100m, 82m, false, ("2023-01-02", "COMPRA LOJA FICTICIA", -18m)))).StatusCode.Should().Be(HttpStatusCode.OK);

        var conciliacoes = await _client.GetFromJsonAsync<JsonElement>($"/api/v1/reconciliations/bank?accountId={accountId}");
        var sugestao = conciliacoes.EnumerateArray().First(c => c.GetProperty("financialTransactionId").ValueKind != JsonValueKind.Null);
        (await _client.PostAsync($"/api/v1/reconciliations/bank/{sugestao.GetProperty("id").GetGuid()}/accept", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var id = (await ListAsync("/api/v1/bank-statements"))[0].GetProperty("id").GetGuid();
        var response = await _client.DeleteAsync($"/api/v1/bank-statements/{id}");

        await ShouldBeRuleAsync(response, "extrato-com-conciliacao-aceita");
        (await ListAsync("/api/v1/bank-statements")).GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task Excluir_fatura_sem_conciliacao_aceita()
    {
        await AuthenticateAsync();
        var cardId = await CreateCardAsync();
        (await _client.PostAsJsonAsync("/api/v1/card-invoices/import",
            new ImportCardInvoiceRequest(cardId, new DateOnly(2026, 9, 2), new DateOnly(2026, 9, 10), 50m, null,
                [new CardInvoiceItemRequest(new DateOnly(2026, 8, 20), "LOJA", null, 50m)]))).StatusCode.Should().Be(HttpStatusCode.OK);
        var id = (await ListAsync($"/api/v1/card-invoices?creditCardId={cardId}"))[0].GetProperty("id").GetGuid();

        (await _client.DeleteAsync($"/api/v1/card-invoices/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ListAsync($"/api/v1/card-invoices?creditCardId={cardId}")).GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Nao_deve_excluir_extrato_de_outro_grupo()
    {
        await AuthenticateAsync();
        var accountId = await CreateAccountAsync();
        (await _client.PostAsJsonAsync("/api/v1/bank-statements/import",
            Statement(accountId, 100m, 82m, false, ("2023-01-02", "COMPRA", -18m)))).StatusCode.Should().Be(HttpStatusCode.OK);
        var id = (await ListAsync("/api/v1/bank-statements"))[0].GetProperty("id").GetGuid();

        await AuthenticateAsync();   // outro usuário, outro grupo

        (await _client.DeleteAsync($"/api/v1/bank-statements/{id}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
