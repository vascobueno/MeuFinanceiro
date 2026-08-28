using MeuFinanceiro.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MeuFinanceiro.Application.IntegrationTests;

/// <summary>
/// Sobe a API completa (HTTP -> Controllers -> Application -> Infrastructure -> EF Core)
/// trocando o PostgreSQL por EF Core InMemory, para testes de integração isolados e
/// rápidos em CI (docs/20-testes.md, seção 3). Para testes contra PostgreSQL real, aponte
/// a connection string de teste via variável de ambiente e remova a troca de provider.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    // Precisa ser fixo por instância da factory (não gerado dentro do options => ...
    // abaixo): esse callback roda a cada nova instância de AppDbContext (ou seja, a cada
    // scope/requisição HTTP), então um Guid.NewGuid() ali dentro criava um banco InMemory
    // novo e vazio por requisição — dados gravados numa chamada nunca apareciam na próxima.
    private readonly string _databaseName = $"MeuFinanceiro.Tests.{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            // Remover só o DbContextOptions<AppDbContext> não basta a partir do EF Core 7+:
            // a configuração do provider (UseNpgsql) fica registrada separadamente como
            // IDbContextOptionsConfiguration<AppDbContext>, e sobra junto com a do InMemory
            // adicionada abaixo, causando "Only a single database provider can be registered".
            var descriptors = services
                .Where(d =>
                    d.ServiceType == typeof(DbContextOptions<AppDbContext>) ||
                    (d.ServiceType.IsGenericType &&
                     d.ServiceType.GetGenericTypeDefinition() == typeof(IDbContextOptionsConfiguration<>) &&
                     d.ServiceType.GenericTypeArguments[0] == typeof(AppDbContext)))
                .ToList();
            foreach (var descriptor in descriptors) services.Remove(descriptor);

            services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(_databaseName));
        });

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "chave-de-teste-somente-para-integration-tests-0123456789",
                ["Jwt:Issuer"] = "MeuFinanceiro.Api.Tests",
                ["Jwt:Audience"] = "MeuFinanceiro.Frontend.Tests",
            });
        });
    }
}
