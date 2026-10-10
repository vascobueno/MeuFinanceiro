using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using MeuFinanceiro.Application.Accounts;
using MeuFinanceiro.Application.Common.Exceptions;
using Xunit;

namespace MeuFinanceiro.Application.IntegrationTests;

/// <summary>Banco, agência e conta, e o limite do cheque especial (docs/04, docs/05).</summary>
[Trait("Category", "Integration")]
public class AccountBankDetailsEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AccountBankDetailsEndpointTests(CustomWebApplicationFactory factory) => _factory = factory;

    private async Task<HttpClient> NovoUsuarioAsync()
    {
        var id = Guid.NewGuid().ToString("N")[..12];
        var (client, _) = await AuthTestHelpers.RegisterConfirmAndLoginAsync(
            _factory, "Usuário Conta", $"conta{id}", $"conta-{id}@teste.com", "SenhaForte!123");
        return client;
    }

    private static object Conta(string nome, string? banco = "033", string? agencia = "1", string? digitoAgencia = null,
        string? numero = "12.345", string? digito = "6", decimal limite = 0, decimal saldoInicial = 100m) => new
    {
        name = nome, bank = "Banco Ficticio", type = 1, initialBalance = saldoInicial,
        bankCode = banco, agency = agencia, agencyDigit = digitoAgencia, accountNumber = numero, accountDigit = digito,
        overdraftLimit = limite,
    };

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();

    private static async Task ShouldBeRuleAsync(HttpResponseMessage response, string rule)
    {
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await JsonAsync(response)).GetProperty("type").GetString().Should().EndWith("/" + rule);
    }

    [Fact]
    public async Task Deve_guardar_dados_bancarios_normalizados_e_o_disponivel()
    {
        var client = await NovoUsuarioAsync();
        var response = await client.PostAsJsonAsync("/api/v1/accounts", Conta("Corrente", digitoAgencia: "x", limite: 500m));
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var conta = await JsonAsync(response);
        conta.GetProperty("bankCode").GetString().Should().Be("033");
        conta.GetProperty("agency").GetString().Should().Be("0001");
        conta.GetProperty("agencyDigit").GetString().Should().Be("X");
        conta.GetProperty("accountNumber").GetString().Should().Be("12345");
        conta.GetProperty("accountDigit").GetString().Should().Be("6");
        conta.GetProperty("overdraftLimit").GetDecimal().Should().Be(500m);
        // O limite não entra no saldo; só no disponível.
        conta.GetProperty("currentBalance").GetDecimal().Should().Be(100m);
        conta.GetProperty("availableBalance").GetDecimal().Should().Be(600m);
    }

    [Fact]
    public async Task Conta_sem_dados_bancarios_continua_valendo()
    {
        var client = await NovoUsuarioAsync();
        var response = await client.PostAsJsonAsync("/api/v1/accounts",
            new { name = "Carteira", type = 1, initialBalance = 50m });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var conta = await JsonAsync(response);
        conta.GetProperty("accountNumber").ValueKind.Should().Be(JsonValueKind.Null);
        conta.GetProperty("availableBalance").GetDecimal().Should().Be(50m);
    }

    [Fact]
    public async Task Dados_bancarios_pela_metade_sao_recusados()
    {
        var client = await NovoUsuarioAsync();
        await ShouldBeRuleAsync(await client.PostAsJsonAsync("/api/v1/accounts", Conta("Sem agência", agencia: null)),
            "dados-bancarios-incompletos");
        await ShouldBeRuleAsync(await client.PostAsJsonAsync("/api/v1/accounts", Conta("Dígito ruim", digito: "Y")),
            "digito-invalido");
    }

    [Fact]
    public async Task Mesma_agencia_e_conta_duas_vezes_no_grupo_e_recusada()
    {
        var client = await NovoUsuarioAsync();
        (await client.PostAsJsonAsync("/api/v1/accounts", Conta("Primeira"))).StatusCode.Should().Be(HttpStatusCode.Created);

        // Mesma conta escrita de outro jeito: "0001" e "12345" sem ponto.
        await ShouldBeRuleAsync(await client.PostAsJsonAsync("/api/v1/accounts", Conta("Segunda", agencia: "0001", numero: "12345")),
            "conta-duplicada");
    }

    [Fact]
    public async Task Editar_para_a_conta_de_outra_e_recusado_mas_salvar_a_propria_nao()
    {
        var client = await NovoUsuarioAsync();
        await client.PostAsJsonAsync("/api/v1/accounts", Conta("Primeira"));
        var segunda = await JsonAsync(await client.PostAsJsonAsync("/api/v1/accounts", Conta("Segunda", numero: "999")));
        var id = segunda.GetProperty("id").GetGuid();

        object Edicao(string numero) => new
        {
            name = "Segunda", bank = "Banco Ficticio", type = 1, initialBalance = 100m, isActive = true,
            bankCode = "033", agency = "1", accountNumber = numero, accountDigit = "6", overdraftLimit = 0m,
        };

        // Salvar com os próprios dados não pode esbarrar nela mesma.
        (await client.PutAsJsonAsync($"/api/v1/accounts/{id}", Edicao("999"))).StatusCode.Should().Be(HttpStatusCode.OK);
        // Trocar para a agência e conta da Primeira, sim.
        await ShouldBeRuleAsync(await client.PutAsJsonAsync($"/api/v1/accounts/{id}", Edicao("12345")), "conta-duplicada");
    }

    [Fact]
    public async Task Mesma_agencia_e_conta_em_outro_grupo_e_permitida()
    {
        var a = await NovoUsuarioAsync();
        var b = await NovoUsuarioAsync();
        (await a.PostAsJsonAsync("/api/v1/accounts", Conta("Do A"))).StatusCode.Should().Be(HttpStatusCode.Created);
        (await b.PostAsJsonAsync("/api/v1/accounts", Conta("Do B"))).StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Limite_negativo_e_recusado()
    {
        var client = await NovoUsuarioAsync();
        await ShouldBeRuleAsync(await client.PostAsJsonAsync("/api/v1/accounts", Conta("Limite", limite: -1m)), "limite-invalido");
    }

    // ---------------- BankDetails puro ----------------

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("33", "1", null, "01.234.567", "x", "033", "0001", null, "01234567", "X")]
    [InlineData("341", "0123", "4", "12345", "0", "341", "0123", "4", "12345", "0")]
    public void Normaliza_pontuacao_e_zeros(string code, string ag, string? agDig, string num, string dig,
        string eCode, string eAg, string? eAgDig, string eNum, string eDig)
    {
        BankDetails.Parse(code, ag, agDig, num, dig).Should().Be(new BankDetails(eCode, eAg, eAgDig, eNum, eDig));
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("1234", "1", "1", "6", "banco-invalido")]
    [InlineData("033", "12345", "1", "6", "agencia-invalida")]
    [InlineData("033", "1", "12345678901234", "6", "conta-invalida")]
    [InlineData("033", "1", "1", "66", "digito-invalido")]
    [InlineData("033", "1", null, "6", "dados-bancarios-incompletos")]
    public void Recusa_dados_invalidos(string? code, string? ag, string? num, string? dig, string rule)
    {
        var act = () => BankDetails.Parse(code, ag, null, num, dig);
        act.Should().Throw<BusinessRuleException>().Which.ErrorType.Should().Be(rule);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Tudo_em_branco_e_sem_dados_bancarios()
    {
        BankDetails.Parse(" ", null, "", null, null).IsEmpty.Should().BeTrue();
    }
}
