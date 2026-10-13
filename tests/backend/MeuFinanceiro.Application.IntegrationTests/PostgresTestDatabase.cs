using MeuFinanceiro.Application.Common.Interfaces;
using MeuFinanceiro.Domain.Enums;
using MeuFinanceiro.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace MeuFinanceiro.Application.IntegrationTests;

/// <summary>
/// PostgreSQL de verdade para os testes de integração (docs/20): um container por execução da
/// suíte, um banco modelo com todas as migrations e uma cópia dele por classe de teste.
///
/// O modelo existe por tempo: rodar as migrations em cada um dos bancos custaria dezenas de
/// segundos; <c>CREATE DATABASE ... TEMPLATE</c> copia em milissegundos. E é uma prova a mais —
/// migration quebrada derruba a suíte logo na subida, com a mensagem do PostgreSQL.
///
/// O container sai sozinho no fim (Ryuk do Testcontainers), até se o processo morrer. É outro
/// container, numa porta aleatória: o banco de desenvolvimento (docker-compose) não é tocado.
/// </summary>
public static class PostgresTestDatabase
{
    private const string Template = "mf_template";

    private static readonly Lazy<Task<PostgreSqlContainer>> Container = new(StartAsync);

    private static async Task<PostgreSqlContainer> StartAsync()
    {
        var container = new PostgreSqlBuilder()
            .WithImage("postgres:18") // a mesma do docker-compose.yml
            // As classes rodam em paralelo, cada uma com o próprio banco e pool: o padrão de 100
            // conexões não chega.
            .WithCommand("-c", "max_connections=300")
            .Build();
        try
        {
            await container.StartAsync();
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            throw new InvalidOperationException(
                "Docker não encontrado ou parado: os testes de integração usam PostgreSQL em container " +
                "(docs/23-configuracao-local.md). Abra o Docker Desktop e rode de novo.", e);
        }

        await ExecuteAsync(container.GetConnectionString(), $"CREATE DATABASE {Template}");
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionStringFor(container, Template))
            .Options;
        await using (var db = new AppDbContext(options, new NoUser()))
        {
            await db.Database.MigrateAsync();
        }
        // Um modelo com conexão aberta não pode ser copiado.
        NpgsqlConnection.ClearAllPools();
        return container;
    }

    /// <summary>Um banco novo, cópia do modelo; devolve o nome e a connection string.</summary>
    public static async Task<(string Name, string ConnectionString)> CreateDatabaseAsync()
    {
        var container = await Container.Value;
        var name = $"mf_{Guid.NewGuid():N}";
        await ExecuteAsync(container.GetConnectionString(), $"CREATE DATABASE {name} TEMPLATE {Template}");
        return (name, ConnectionStringFor(container, name));
    }

    public static async Task DropDatabaseAsync(string name)
    {
        if (string.IsNullOrEmpty(name) || !Container.IsValueCreated) return;
        var container = await Container.Value;
        await ExecuteAsync(container.GetConnectionString(), $"DROP DATABASE IF EXISTS {name} WITH (FORCE)");
    }

    private static string ConnectionStringFor(PostgreSqlContainer container, string database) =>
        new NpgsqlConnectionStringBuilder(container.GetConnectionString())
        {
            Database = database,
            // 10 por classe dá folga dentro do max_connections do container.
            MaxPoolSize = 10,
        }.ConnectionString;

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        // Sem pool: são comandos de administração, e uma conexão parada no modelo impediria a cópia.
        var builder = new NpgsqlConnectionStringBuilder(connectionString) { Pooling = false };
        await using var connection = new NpgsqlConnection(builder.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>As migrations não dependem do usuário logado; o contexto só exige um.</summary>
    private sealed class NoUser : ICurrentUserService
    {
        public Guid UserId => Guid.Empty;
        public Guid GrupoId => Guid.Empty;
        public GrupoRole GrupoRole => default;
        public GrupoAccessProfile AccessProfile => default;
        public bool IsAuthenticated => false;
        public bool IsAdministrator => false;
        public bool HasFullAccess => false;
        public bool CanWrite => false;
    }
}
