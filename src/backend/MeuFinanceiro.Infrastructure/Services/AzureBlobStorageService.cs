using Azure.Storage.Blobs;
using MeuFinanceiro.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;

namespace MeuFinanceiro.Infrastructure.Services;

/// <summary>
/// Azure Blob Storage em produção / Azurite em dev (docs/00-visao-geral.md,
/// docs/23-configuracao-local.md). Containers são privados por padrão (docs/12-seguranca.md);
/// nenhuma URL pública é gerada aqui.
/// </summary>
public class AzureBlobStorageService : IFileStorageService
{
    private readonly BlobServiceClient _blobServiceClient;

    public AzureBlobStorageService(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("BlobStorage")
            ?? throw new InvalidOperationException("Connection string 'BlobStorage' não configurada.");
        _blobServiceClient = new BlobServiceClient(connectionString);
    }

    public async Task<string> SaveAsync(string containerName, string fileName, Stream content, string contentType, CancellationToken ct = default)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        await containerClient.CreateIfNotExistsAsync(cancellationToken: ct);

        var blobClient = containerClient.GetBlobClient(fileName);
        await blobClient.UploadAsync(content, overwrite: true, cancellationToken: ct);

        return fileName;
    }

    public async Task<Stream> OpenReadAsync(string containerName, string blobPath, CancellationToken ct = default)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        var blobClient = containerClient.GetBlobClient(blobPath);
        var download = await blobClient.DownloadStreamingAsync(cancellationToken: ct);
        return download.Value.Content;
    }
}
