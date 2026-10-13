using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using FluentAssertions;
using MeuFinanceiro.Application.Accounts;
using MeuFinanceiro.Application.Auth;
using MeuFinanceiro.Application.Common.Interfaces;
using MeuFinanceiro.Application.Grupos;
using MeuFinanceiro.Domain.Entities;
using MeuFinanceiro.Domain.Enums;
using MeuFinanceiro.Infrastructure.Persistence;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Xunit;

namespace MeuFinanceiro.Application.IntegrationTests;

/// <summary>
/// O que só um banco de verdade garante (docs/20): transação, índice único, check constraint e a
/// disputa por um fechamento mensal (forçada, para não depender de sorte). Com o banco em memória de antes, nenhum destes testes era
/// possível — ele ignorava os quatro. Dados sintéticos.
/// </summary>
[Trait("Category", "Integration")]
public class PostgresConstraintTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public PostgresConstraintTests(CustomWebApplicationFactory factory) => _factory = factory;

    private async Task<HttpClient> NovoUsuarioAsync()
    {
        var id = Guid.NewGuid().ToString("N")[..12];
        var (client, _) = await AuthTestHelpers.RegisterConfirmAndLoginAsync(
            _factory, "Usuário Banco", $"banco{id}", $"banco-{id}@teste.com", "SenhaForte!123");
        return client;
    }

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.Should().BeTrue(body);
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private async Task<T> NoBancoAsync<T>(Func<AppDbContext, Task<T>> acao)
    {
        using var scope = _factory.Services.CreateScope();
        return await acao(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private static string? SqlState(Exception e) => (e.InnerException as PostgresException)?.SqlState;

    /// <summary>Grupo de usuários criado pela API; devolve o grupo de cartões e o GrupoId dele.</summary>
    private async Task<(Guid cartao, Guid grupoId)> GrupoDeCartoesAsync()
    {
        var client = await NovoUsuarioAsync();
        var cartao = (await JsonAsync(await client.PostAsJsonAsync("/api/v1/cards",
            new { name = $"Grupo {Guid.NewGuid():N}"[..20], brand = "Visa", closingDay = 2, dueDay = 10 }))).GetProperty("id").GetGuid();
        var grupoId = await NoBancoAsync(db => db.CreditCards.Where(c => c.Id == cartao).Select(c => c.GrupoId).SingleAsync());
        return (cartao, grupoId);
    }

    [Fact]
    public async Task Cadastro_que_falha_ao_criar_o_grupo_nao_deixa_usuario_orfao()
    {
        // A criação do grupo falha depois de o usuário já ter sido gravado: a transação do
        // AuthService precisa desfazer os dois.
        using var comFalha = _factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.RemoveAll<IGrupoService>();
            s.AddScoped(_ => DispatchProxy.Create<IGrupoService, GrupoQueFalha>());
        }));
        var username = $"orfao{Guid.NewGuid():N}"[..17];

        var resposta = await comFalha.CreateClient().PostAsJsonAsync("/api/v1/auth/register",
            new RegisterRequest("Usuário Órfão", username, $"{username}@teste.com", "SenhaForte!123"));

        resposta.IsSuccessStatusCode.Should().BeFalse();
        (await NoBancoAsync(db => db.Users.AnyAsync(u => u.UserName == username))).Should().BeFalse();
    }

    [Fact]
    public async Task Indice_recusa_dois_cartoes_ativos_com_o_mesmo_final_mesmo_sem_passar_pelo_servico()
    {
        var (cartao, grupoId) = await GrupoDeCartoesAsync();

        var acao = () => NoBancoAsync(async db =>
        {
            db.IssuedCards.Add(new IssuedCard { GrupoId = grupoId, CreditCardId = cartao, LastFour = "4051" });
            db.IssuedCards.Add(new IssuedCard { GrupoId = grupoId, CreditCardId = cartao, LastFour = "4051", Kind = IssuedCardKind.Adicional });
            return await db.SaveChangesAsync();
        });

        (await acao.Should().ThrowAsync<DbUpdateException>()).Which.Should().Match<DbUpdateException>(e => SqlState(e) == PostgresErrorCodes.UniqueViolation);
    }

    [Fact]
    public async Task Indice_do_final_vale_so_entre_ativos()
    {
        var (cartao, grupoId) = await GrupoDeCartoesAsync();

        var gravados = await NoBancoAsync(async db =>
        {
            db.IssuedCards.Add(new IssuedCard { GrupoId = grupoId, CreditCardId = cartao, LastFour = "4052", Status = IssuedCardStatus.Cancelado });
            db.IssuedCards.Add(new IssuedCard { GrupoId = grupoId, CreditCardId = cartao, LastFour = "4052" });
            return await db.SaveChangesAsync();
        });

        gravados.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task Check_constraint_recusa_cartao_sem_credito_nem_debito()
    {
        var (_, grupoId) = await GrupoDeCartoesAsync();

        var acao = () => NoBancoAsync(async db =>
        {
            db.IssuedCards.Add(new IssuedCard { GrupoId = grupoId, LastFour = "4053" });
            return await db.SaveChangesAsync();
        });

        (await acao.Should().ThrowAsync<DbUpdateException>()).Which.Should().Match<DbUpdateException>(e => SqlState(e) == PostgresErrorCodes.CheckViolation);
    }

    [Fact]
    public async Task Leitura_do_saldo_que_perde_a_disputa_pelo_fechamento_nao_quebra()
    {
        var client = await NovoUsuarioAsync();
        var conta = (await JsonAsync(await client.PostAsJsonAsync("/api/v1/accounts",
            new { name = $"Conta {Guid.NewGuid():N}"[..20], type = 1, initialBalance = 1000m }))).GetProperty("id").GetGuid();
        foreach (var mes in new[] { "2025-01-10", "2025-06-10", "2026-02-10" })
        {
            await JsonAsync(await client.PostAsJsonAsync("/api/v1/transactions", new
            {
                transactionDate = mes, description = "Loja Ficticia", accountId = conta, type = 2, direction = 1, amount = 10m, status = 2,
            }));
        }
        var (grupoId, connectionString) = await NoBancoAsync(async db =>
            (await db.Accounts.Where(a => a.Id == conta).Select(a => a.GrupoId).SingleAsync(), db.Database.GetConnectionString()!));
        await NoBancoAsync(db => db.AccountMonthlyBalances.Where(b => b.AccountId == conta).ExecuteDeleteAsync());
        var usuario = new UsuarioFixo(grupoId);

        // Disputa forçada, sem depender de sorte: bem na hora de a leitura A gravar os fechamentos
        // que calculou, a leitura B calcula e grava os mesmos primeiro. A esbarra no índice único
        // (AccountId, PeriodEnd) — e precisa devolver o saldo mesmo assim.
        decimal? saldoB = null;
        var antesDeGravar = new AntesDeGravar(async () =>
        {
            await using var dbB = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options, usuario);
            saldoB = (await new AccountBalanceService(dbB, usuario).GetBalancesAsync(new[] { conta }))[conta];
        });
        await using var dbA = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).AddInterceptors(antesDeGravar).Options, usuario);

        var saldoA = (await new AccountBalanceService(dbA, usuario).GetBalancesAsync(new[] { conta }))[conta];

        antesDeGravar.Disparou.Should().BeTrue();
        saldoA.Should().Be(saldoB);
        (await NoBancoAsync(db => db.AccountMonthlyBalances.Where(b => b.AccountId == conta)
            .GroupBy(b => b.PeriodEnd).AnyAsync(g => g.Count() > 1))).Should().BeFalse();
    }

    /// <summary>Roda a ação uma vez, logo antes da primeira gravação do contexto.</summary>
    private sealed class AntesDeGravar(Func<Task> acao) : SaveChangesInterceptor
    {
        public bool Disparou { get; private set; }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (!Disparou)
            {
                Disparou = true;
                await acao();
            }
            return result;
        }
    }

    private sealed class UsuarioFixo(Guid grupoId) : ICurrentUserService
    {
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid GrupoId => grupoId;
        public GrupoRole GrupoRole => GrupoRole.Owner;
        public GrupoAccessProfile AccessProfile => GrupoAccessProfile.Completo;
        public bool IsAuthenticated => true;
        public bool IsAdministrator => false;
        public bool HasFullAccess => true;
        public bool CanWrite => true;
    }

    /// <summary>Todo método do serviço de grupos falha — só a criação do grupo é chamada no cadastro.</summary>
    public class GrupoQueFalha : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            throw new InvalidOperationException("Falha simulada ao criar o grupo.");
    }
}
