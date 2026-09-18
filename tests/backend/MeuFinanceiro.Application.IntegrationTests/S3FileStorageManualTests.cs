using FluentAssertions;
using MeuFinanceiro.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace MeuFinanceiro.Application.IntegrationTests;

/// <summary>
/// Exercita o S3FileStorageService contra o MinIO de verdade (docker-compose.yml).
///
/// Fora das categorias Unit/Integration de propósito: o CI não sobe serviço nenhum
/// (.github/workflows/ci.yml), e o resto da suíte usa InMemoryFileStorage justamente para não
/// depender disto. Este é o único teste que cobre o adaptador real — sem ele, um erro de
/// protocolo (path-style, bucket ausente, delete que não apaga) só apareceria em produção.
///
/// Rodar à mão, com o compose no ar:
///   docker compose up -d minio
///   dotnet test tests/backend/MeuFinanceiro.Application.IntegrationTests --filter "Category=Manual"
/// </summary>
[Trait("Category", "Manual")]
public class S3FileStorageManualTests
{
    private static S3FileStorageService CriarServico()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:ServiceUrl"] = "http://localhost:9000",
                ["Storage:AccessKey"] = "minioadmin",
                ["Storage:SecretKey"] = "minioadmin123",
            })
            .Build();

        return new S3FileStorageService(configuration);
    }

    [Fact]
    public async Task Gravar_ler_e_apagar_devem_funcionar_contra_o_MinIO()
    {
        var storage = CriarServico();
        const string container = "attachments";
        var key = $"teste-manual/{Guid.NewGuid()}.pdf";
        var conteudo = "%PDF-1.7\nteste de anexo\n%%EOF"u8.ToArray();

        await storage.SaveAsync(container, key, new MemoryStream(conteudo), "application/pdf");

        await using (var lido = await storage.OpenReadAsync(container, key))
        {
            using var memory = new MemoryStream();
            await lido.CopyToAsync(memory);
            memory.ToArray().Should().Equal(conteudo, "o conteúdo tem de voltar byte a byte");
        }

        await storage.DeleteAsync(container, key);

        // Depois do delete, a leitura tem de falhar — se passar, o objeto continua no bucket.
        var act = async () => await storage.OpenReadAsync(container, key);
        await act.Should().ThrowAsync<Exception>("o objeto não pode continuar no bucket após o DELETE");
    }

    [Fact]
    public async Task Apagar_objeto_inexistente_nao_deve_quebrar()
    {
        // Idempotência: quem chama está removendo a linha do banco, que é a fonte da verdade.
        var storage = CriarServico();

        var act = async () => await storage.DeleteAsync("attachments", $"teste-manual/{Guid.NewGuid()}.pdf");

        await act.Should().NotThrowAsync();
    }
}
