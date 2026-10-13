using MeuFinanceiro.Application.Common.Interfaces;
using MeuFinanceiro.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace MeuFinanceiro.Application.IntegrationTests;

/// <summary>
/// Sobe a API completa (HTTP -> Controllers -> Application -> Infrastructure -> EF Core) contra
/// PostgreSQL de verdade: cada classe de teste ganha o próprio banco, cópia do modelo com as
/// migrations (PostgresTestDatabase, docs/20-testes.md). O provider é o mesmo da produção — só a
/// connection string muda —, então índices únicos, FKs, check constraints, transações e a tradução
/// das consultas para SQL valem nos testes como valem em produção.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private string _databaseName = "";
    private string _connectionString = "";

    /// <summary>Antes do primeiro teste da classe: o banco precisa existir antes de a API subir.</summary>
    public async Task InitializeAsync() =>
        (_databaseName, _connectionString) = await PostgresTestDatabase.CreateDatabaseAsync();

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await PostgresTestDatabase.DropDatabaseAsync(_databaseName);
    }

    public TestEmailSender EmailSender => Services.GetRequiredService<TestEmailSender>();

    public InMemoryFileStorage FileStorage => Services.GetRequiredService<InMemoryFileStorage>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Trava: sem o banco desta classe (InitializeAsync não rodou), a API cairia na connection
        // string do appsettings.Development.json local — o banco de desenvolvimento, com o seed
        // de subida gravando nele.
        if (string.IsNullOrEmpty(_connectionString))
            throw new InvalidOperationException("Banco de teste não criado: use a factory como IClassFixture.");

        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            // O mesmo UseNpgsql da produção, com o banco desta classe. ConfigureDbContext roda depois
            // do AddDbContext da Infrastructure, então vale mais que qualquer appsettings.
            services.ConfigureDbContext<AppDbContext>(options => options.UseNpgsql(_connectionString));

            // Substitui o SmtpEmailSender por um fake em memória — os testes de reset de senha
            // (PasswordResetEndpointTests) leem o token direto de TestEmailSender.LastEmail em
            // vez de precisar de um SMTP de verdade.
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<TestEmailSender>();
            services.AddSingleton<IEmailSender>(sp => sp.GetRequiredService<TestEmailSender>());

            // Substitui o MinIO por um storage em memória: o CI não sobe serviço nenhum, então
            // sem isto todo teste de anexo dependeria de um MinIO local.
            services.RemoveAll<IFileStorageService>();
            services.AddSingleton<InMemoryFileStorage>();
            services.AddSingleton<IFileStorageService>(sp => sp.GetRequiredService<InMemoryFileStorage>());
        });

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Também aqui, para quem ler a configuração direto (a trava acima cobre o DbContext).
                ["ConnectionStrings:DefaultConnection"] = _connectionString,
                ["Jwt:Key"] = "chave-de-teste-somente-para-integration-tests-0123456789",
                ["Jwt:Issuer"] = "MeuFinanceiro.Api.Tests",
                ["Jwt:Audience"] = "MeuFinanceiro.Frontend.Tests",
                // Explícito pra não depender de um appsettings.Development.json local (gitignored,
                // ausente num checkout limpo como o do CI) — sem isso, PasswordResetEndpointTests
                // pegava o "" comitado em appsettings.json e gerava um link relativo quebrado.
                ["App:FrontendBaseUrl"] = "http://localhost:4200",
                // O limite de produção (10/5min) é pensado pra um IP de verdade — o TestServer
                // não tem um, então muitos testes no mesmo fixture compartilhariam a partição
                // "anonymous" e esbarrariam no limite. Afrouxado só pra não interferir nos testes.
                ["RateLimiting:Auth:PermitLimit"] = "100000",
                // Mesmo motivo do de cima, para o limitador global: sem IP real, a suíte inteira
                // compartilha a partição "anonymous" e estoura o teto de 200/min de produção.
                ["RateLimiting:Global:PermitLimit"] = "1000000",
                ["RateLimiting:Auth:WindowMinutes"] = "1",
            });
        });
    }
}
