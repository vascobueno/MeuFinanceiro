using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace MeuFinanceiro.Application.IntegrationTests;

/// <summary>
/// Cobre o cálculo de saldo (docs/05, regra 7) e o cache de fechamento mensal.
///
/// Fecha duas lacunas que a suíte não tinha: o saldo só era exercitado de forma indireta, e a
/// LISTAGEM de recorrências (cujo contrato passou a ser paginado) não era testada — só o
/// GET por id.
/// </summary>
[Trait("Category", "Integration")]
public class AccountBalanceEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AccountBalanceEndpointTests(CustomWebApplicationFactory factory) => _factory = factory;

    /// <summary>Usuário novo (e portanto grupo novo) por teste, para um não enxergar o saldo do outro.</summary>
    private async Task<(HttpClient client, MeuFinanceiro.Application.Auth.UserResponse user)> NovoUsuarioAsync()
    {
        var id = Guid.NewGuid().ToString("N")[..12];
        return await AuthTestHelpers.RegisterConfirmAndLoginAsync(
            _factory, "Usuário Saldo", $"saldo{id}", $"saldo-{id}@teste.com", "SenhaForte!123");
    }

    private static async Task<Guid> CriarContaAsync(HttpClient client, decimal saldoInicial)
    {
        var response = await client.PostAsJsonAsync("/api/v1/accounts", new
        {
            name = $"Conta {Guid.NewGuid():N}"[..20],
            bank = "Banco Teste",
            type = 1,
            initialBalance = saldoInicial
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<decimal> SaldoAtualAsync(HttpClient client, Guid accountId)
    {
        var response = await client.GetAsync($"/api/v1/accounts/{accountId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("currentBalance").GetDecimal();
    }

    private static async Task LancarAsync(HttpClient client, Guid accountId, string data, decimal valor, int status)
    {
        var criar = await client.PostAsJsonAsync("/api/v1/transactions", new
        {
            transactionDate = data,
            description = $"Lancamento {Guid.NewGuid():N}"[..24],
            accountId,
            type = 2,        // Expense
            direction = 1,   // Debit
            amount = valor
        });
        criar.StatusCode.Should().Be(HttpStatusCode.OK);

        if (status == 1) return; // nasce Pending; nada a promover

        var criados = JsonDocument.Parse(await criar.Content.ReadAsStringAsync());
        var id = criados.RootElement[0].GetProperty("id").GetGuid();

        var atualizar = await client.PutAsJsonAsync($"/api/v1/transactions/{id}", new
        {
            transactionDate = data,
            description = criados.RootElement[0].GetProperty("description").GetString(),
            accountId,
            direction = 1,
            amount = valor,
            status
        });
        atualizar.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Saldo_de_conta_nova_deve_ser_o_saldo_inicial()
    {
        var (client, _) = await NovoUsuarioAsync();
        var contaId = await CriarContaAsync(client, 1000m);

        (await SaldoAtualAsync(client, contaId)).Should().Be(1000m);
    }

    [Fact]
    public async Task Lancamento_pendente_nao_deve_entrar_no_saldo()
    {
        var (client, _) = await NovoUsuarioAsync();
        var contaId = await CriarContaAsync(client, 1000m);

        await LancarAsync(client, contaId, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-10).ToString("yyyy-MM-dd"), 250m, status: 1);

        (await SaldoAtualAsync(client, contaId)).Should().Be(1000m);
    }

    [Fact]
    public async Task Lancamento_confirmado_no_passado_deve_reduzir_o_saldo()
    {
        var (client, _) = await NovoUsuarioAsync();
        var contaId = await CriarContaAsync(client, 1000m);

        await LancarAsync(client, contaId, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-10).ToString("yyyy-MM-dd"), 250m, status: 2);

        (await SaldoAtualAsync(client, contaId)).Should().Be(750m);
    }

    [Fact]
    public async Task Lancamento_confirmado_com_data_futura_nao_deve_entrar_no_saldo_atual()
    {
        // Regressão: a tela de Contas somava tudo, inclusive datas futuras, enquanto a Projeção
        // cortava em hoje — as duas mostravam "saldo atual" diferente para a mesma conta.
        var (client, _) = await NovoUsuarioAsync();
        var contaId = await CriarContaAsync(client, 1000m);

        await LancarAsync(client, contaId, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30).ToString("yyyy-MM-dd"), 400m, status: 2);

        (await SaldoAtualAsync(client, contaId)).Should().Be(1000m);
    }

    [Fact]
    public async Task Lancamento_em_mes_antigo_deve_invalidar_o_fechamento_e_refletir_no_saldo()
    {
        // O fechamento mensal é cache: escrever num mês já fechado tem de invalidá-lo, senão o
        // saldo fica defasado em silêncio.
        var (client, _) = await NovoUsuarioAsync();
        var contaId = await CriarContaAsync(client, 5000m);

        var mesAntigo = DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(-4).ToString("yyyy-MM-dd");
        await LancarAsync(client, contaId, mesAntigo, 100m, status: 2);

        var saldoAposPrimeiro = await SaldoAtualAsync(client, contaId);
        saldoAposPrimeiro.Should().Be(4900m);

        // Segundo lançamento no MESMO mês já fechado, depois do cache ter sido gerado pela leitura.
        await LancarAsync(client, contaId, mesAntigo, 300m, status: 2);

        (await SaldoAtualAsync(client, contaId)).Should().Be(4600m);
    }

    [Fact]
    public async Task Verificacao_de_saldos_deve_conferir_o_cache_contra_a_soma_bruta()
    {
        var (client, _) = await NovoUsuarioAsync();
        var contaId = await CriarContaAsync(client, 2000m);

        await LancarAsync(client, contaId, DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(-3).ToString("yyyy-MM-dd"), 150m, status: 2);
        await LancarAsync(client, contaId, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2).ToString("yyyy-MM-dd"), 50m, status: 2);
        _ = await SaldoAtualAsync(client, contaId); // gera o cache

        var response = await client.GetAsync("/api/v1/accounts/balances/verify");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.EnumerateArray()
            .Should().OnlyContain(c => c.GetProperty("ok").GetBoolean(), "o cache não pode divergir da soma bruta");
    }

    [Fact]
    public async Task Listagem_de_recorrencias_deve_vir_paginada()
    {
        // O contrato deste endpoint mudou de array para PagedResult; a suíte só cobria o GET por id.
        var (client, _) = await NovoUsuarioAsync();

        var response = await client.GetAsync("/api/v1/recurring-transactions?page=1&pageSize=10");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.TryGetProperty("items", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("totalItems", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("totalPages", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Relatorio_de_recorrencias_deve_vir_sem_paginacao()
    {
        // O relatório é imprimível: o total do rodapé precisa fechar sobre o conjunto inteiro.
        var (client, _) = await NovoUsuarioAsync();

        var response = await client.GetAsync("/api/v1/recurring-transactions/report?type=2");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
    }
}
