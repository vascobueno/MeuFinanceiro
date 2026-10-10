using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace MeuFinanceiro.Application.IntegrationTests;

/// <summary>
/// Tipo do movimento e data da compra no débito, do texto colado até a conciliação (docs/09).
/// </summary>
[Trait("Category", "Integration")]
public class StatementClassificationEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public StatementClassificationEndpointTests(CustomWebApplicationFactory factory) => _factory = factory;

    private async Task<HttpClient> NovoUsuarioAsync()
    {
        var id = Guid.NewGuid().ToString("N")[..12];
        var (client, _) = await AuthTestHelpers.RegisterConfirmAndLoginAsync(
            _factory, "Usuário Extrato", $"extrato{id}", $"extrato-{id}@teste.com", "SenhaForte!123");
        return client;
    }

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response)
    {
        response.IsSuccessStatusCode.Should().BeTrue(await response.Content.ReadAsStringAsync());
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
    }

    private static async Task<Guid> CriarContaAsync(HttpClient client) =>
        (await JsonAsync(await client.PostAsJsonAsync("/api/v1/accounts",
            new { name = $"Conta {Guid.NewGuid():N}"[..20], bank = "Banco Teste", type = 1, initialBalance = 1000m })))
        .GetProperty("id").GetGuid();

    private static async Task<JsonElement> ImportarAsync(HttpClient client, Guid contaId, object itens) =>
        await JsonAsync(await client.PostAsJsonAsync("/api/v1/bank-statements/import", new
        {
            accountId = contaId,
            referenceStart = "2023-01-01",
            referenceEnd = "2023-01-31",
            openingBalance = 1000m,
            closingBalance = 0m,
            confirmUnbalanced = true,
            items = itens,
        }));

    private static async Task<List<JsonElement>> ConciliacoesAsync(HttpClient client, Guid contaId) =>
        (await JsonAsync(await client.GetAsync($"/api/v1/reconciliations/bank?accountId={contaId}"))).EnumerateArray().ToList();

    [Fact]
    public async Task Compra_no_debito_lancada_seis_dias_depois_concilia_pela_data_da_compra()
    {
        var client = await NovoUsuarioAsync();
        var contaId = await CriarContaAsync(client);
        // O usuário registra a compra no dia em que comprou.
        await JsonAsync(await client.PostAsJsonAsync("/api/v1/transactions", new
        {
            transactionDate = "2023-01-02", description = "Loja Ficticia A", accountId = contaId, type = 2, direction = 1, amount = 18.00m,
        }));

        var lido = await JsonAsync(await client.PostAsJsonAsync("/api/v1/bank-statements/parse", new
        {
            text = "08/01 COMPRA CARTAO DEB MC 200652 18,00-\n02/01 LOJA FICTICIA A",
            referenceStart = "2023-01-01",
            referenceEnd = "2023-01-31",
        }));
        var item = lido.GetProperty("items").EnumerateArray().Single();
        item.GetProperty("itemType").GetString().Should().Be("DEBIT_CARD_PURCHASE");
        item.GetProperty("effectiveDate").GetString().Should().Be("2023-01-02");

        await ImportarAsync(client, contaId, lido.GetProperty("items"));

        var r = (await ConciliacoesAsync(client, contaId)).Single();
        r.GetProperty("itemType").GetString().Should().Be("DEBIT_CARD_PURCHASE");
        r.GetProperty("itemEffectiveDate").GetString().Should().Be("2023-01-02");
        r.GetProperty("dateDifferenceDays").GetInt32().Should().Be(0);
        // Pela data do lançamento (6 dias depois) o score seria 63 e o item ficaria sem par; pela da
        // compra é 83. Não chega a 90 porque o prefixo do banco deixa a descrição só parecida.
        r.GetProperty("financialTransactionId").ValueKind.Should().Be(JsonValueKind.String);
        r.GetProperty("status").GetString().Should().Be("PossibleMatch");
    }

    [Fact]
    public async Task Movimento_sem_tipo_ou_com_tipo_desconhecido_e_classificado_na_importacao()
    {
        var client = await NovoUsuarioAsync();
        var contaId = await CriarContaAsync(client);

        await ImportarAsync(client, contaId, new object[]
        {
            new { date = "2023-01-10", description = "SAQUE BANCO 24 HORAS", document = (string?)null, itemType = (string?)null, amount = -200m },
            new { date = "2023-01-11", description = "PIX RECEBIDO PORTADOR", document = (string?)null, itemType = "inventado", amount = 50m },
            new { date = "2023-01-12", description = "QUALQUER COISA", document = (string?)null, itemType = "bank_fee", amount = -9.90m },
        });

        var tipos = (await ConciliacoesAsync(client, contaId))
            .ToDictionary(r => r.GetProperty("itemDescription").GetString()!, r => r.GetProperty("itemType").GetString());
        tipos["SAQUE BANCO 24 HORAS"].Should().Be("ATM_WITHDRAWAL");
        tipos["PIX RECEBIDO PORTADOR"].Should().Be("PIX_RECEIVED");
        tipos["QUALQUER COISA"].Should().Be("BANK_FEE");
    }
}
