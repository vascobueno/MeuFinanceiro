using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MeuFinanceiro.Application.Auth;
using MeuFinanceiro.Application.Reconciliation;
using MeuFinanceiro.Application.Statements;
using Xunit;

namespace MeuFinanceiro.Application.IntegrationTests;

/// <summary>
/// Tela Extratos: listagem do que a importação da conciliação bancária gravou, com filtros,
/// ordenação e paginação server-side (regra 19).
///
/// O teste que mais importa aqui é o do isolamento por grupo: não existe HasQueryFilter global
/// no AppDbContext, então o filtro por GrupoId é escrito à mão em cada consulta — e esquecê-lo
/// vazaria extrato de outra família sem quebrar nada visível.
/// </summary>
[Trait("Category", "Integration")]
public class BankStatementsEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public BankStatementsEndpointTests(CustomWebApplicationFactory factory)
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

    private async Task<Guid> CreateAccountAsync(string nome)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/accounts", new
        {
            name = nome,
            bank = "Banco Teste",
            type = 1,
            initialBalance = 0m
        });
        var account = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        return account.GetProperty("id").GetGuid();
    }

    private async Task ImportAsync(Guid accountId, DateOnly fim, decimal inicial, decimal final, int itens, string arquivo)
    {
        var request = new ImportBankStatementRequest(
            accountId,
            fim.AddDays(-30),
            fim,
            inicial,
            final,
            arquivo,
            Enumerable.Range(0, itens)
                .Select(i => new BankStatementItemRequest(fim.AddDays(-i - 1), $"Item {i}", null, "OTHER_EXPENSE", -10m))
                .ToList());

        var response = await _client.PostAsJsonAsync("/api/v1/bank-statements/import", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<PagedResultDto> ListarAsync(string query = "")
    {
        var response = await _client.GetAsync($"/api/v1/bank-statements{query}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<PagedResultDto>())!;
    }

    [Fact]
    public async Task Deve_listar_extrato_importado_com_saldos_movimento_e_contagem_de_itens()
    {
        await AuthenticateAsync();
        var contaId = await CreateAccountAsync("Conta Corrente");
        var fim = DateOnly.FromDateTime(DateTime.UtcNow);

        await ImportAsync(contaId, fim, inicial: 1000m, final: 1250m, itens: 3, arquivo: "extrato.ofx");

        var page = await ListarAsync();

        page.Items.Should().ContainSingle();
        var extrato = page.Items[0];
        extrato.AccountName.Should().Be("Conta Corrente");
        extrato.OpeningBalance.Should().Be(1000m);
        extrato.ClosingBalance.Should().Be(1250m);
        extrato.NetChange.Should().Be(250m);
        extrato.ItemCount.Should().Be(3);
        extrato.OriginalFileName.Should().Be("extrato.ofx");
        extrato.ReferenceEnd.Should().Be(fim);
    }

    [Fact]
    public async Task Filtro_por_conta_deve_devolver_so_os_extratos_dela()
    {
        await AuthenticateAsync();
        var corrente = await CreateAccountAsync("Corrente");
        var poupanca = await CreateAccountAsync("Poupança");
        var fim = DateOnly.FromDateTime(DateTime.UtcNow);

        await ImportAsync(corrente, fim, 100m, 200m, 1, "corrente.ofx");
        await ImportAsync(poupanca, fim, 300m, 400m, 1, "poupanca.ofx");

        var page = await ListarAsync($"?accountId={poupanca}");

        page.Items.Should().ContainSingle();
        page.Items[0].AccountName.Should().Be("Poupança");
    }

    [Fact]
    public async Task Filtro_por_periodo_deve_cortar_pela_data_final_do_extrato()
    {
        await AuthenticateAsync();
        var contaId = await CreateAccountAsync("Conta Período");
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);

        await ImportAsync(contaId, hoje.AddMonths(-3), 10m, 20m, 1, "antigo.ofx");
        await ImportAsync(contaId, hoje, 30m, 40m, 1, "atual.ofx");

        var page = await ListarAsync($"?periodStart={hoje.AddDays(-10):yyyy-MM-dd}");

        page.Items.Should().ContainSingle();
        page.Items[0].OriginalFileName.Should().Be("atual.ofx");
    }

    [Fact]
    public async Task Deve_ordenar_por_saldo_final_decrescente()
    {
        await AuthenticateAsync();
        var contaId = await CreateAccountAsync("Conta Ordenação");
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);

        await ImportAsync(contaId, hoje.AddDays(-20), 0m, 50m, 1, "menor.ofx");
        await ImportAsync(contaId, hoje.AddDays(-10), 0m, 900m, 1, "maior.ofx");
        await ImportAsync(contaId, hoje, 0m, 500m, 1, "meio.ofx");

        var page = await ListarAsync("?sortBy=saldofinal&sortDir=desc");

        page.Items.Select(i => i.OriginalFileName)
            .Should().Equal("maior.ofx", "meio.ofx", "menor.ofx");
    }

    [Fact]
    public async Task Deve_paginar_informando_total_de_itens_e_de_paginas()
    {
        await AuthenticateAsync();
        var contaId = await CreateAccountAsync("Conta Paginação");
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);

        for (var i = 0; i < 5; i++)
        {
            await ImportAsync(contaId, hoje.AddDays(-i * 31), 0m, 100m + i, 1, $"extrato-{i}.ofx");
        }

        var page = await ListarAsync("?page=2&pageSize=2");

        page.Items.Should().HaveCount(2);
        page.TotalItems.Should().Be(5);
        page.TotalPages.Should().Be(3);
        page.Page.Should().Be(2);
    }

    [Fact]
    public async Task Nao_deve_mostrar_extrato_de_outro_grupo()
    {
        // Primeiro usuário: importa um extrato e confirma que ele aparece para si.
        await AuthenticateAsync();
        var contaId = await CreateAccountAsync("Conta do Grupo A");
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        await ImportAsync(contaId, hoje, 0m, 777m, 2, "do-grupo-a.ofx");

        (await ListarAsync()).Items.Should().ContainSingle();

        // Segundo usuário, outro grupo: a lista dele tem de ignorar o extrato acima.
        await AuthenticateAsync();

        var page = await ListarAsync();

        page.Items.Should().BeEmpty();
        page.TotalItems.Should().Be(0);
    }

    private record PagedResultDto(List<BankStatementListItemResponse> Items, int Page, int PageSize, int TotalItems, int TotalPages);
}
