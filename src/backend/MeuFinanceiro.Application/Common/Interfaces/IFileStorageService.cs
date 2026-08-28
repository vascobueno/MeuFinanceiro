namespace MeuFinanceiro.Application.Common.Interfaces;

/// <summary>
/// Abstração de armazenamento de arquivos (Azure Blob Storage em produção, Azurite em dev —
/// docs/00-visao-geral.md, docs/23-configuracao-local.md). Blob privado, uploads validados
/// pelo chamador antes de gravar (docs/12-seguranca.md).
/// </summary>
public interface IFileStorageService
{
    /// <summary>Grava o conteúdo e retorna o caminho/identificador do blob gravado.</summary>
    Task<string> SaveAsync(string containerName, string fileName, Stream content, string contentType, CancellationToken ct = default);

    Task<Stream> OpenReadAsync(string containerName, string blobPath, CancellationToken ct = default);
}
