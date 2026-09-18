namespace MeuFinanceiro.Application.Common.Interfaces;

/// <summary>
/// Abstração de armazenamento de arquivos, falada via protocolo S3 (MinIO em dev/produção, ou
/// qualquer outro serviço S3-compatible — docs/00-visao-geral.md, docs/23-configuracao-local.md).
/// Bucket privado, uploads validados pelo chamador antes de gravar (docs/12-seguranca.md).
/// </summary>
public interface IFileStorageService
{
    /// <summary>Grava o conteúdo e retorna o caminho/identificador do blob gravado.</summary>
    Task<string> SaveAsync(string containerName, string fileName, Stream content, string contentType, CancellationToken ct = default);

    Task<Stream> OpenReadAsync(string containerName, string blobPath, CancellationToken ct = default);

    /// <summary>
    /// Remove o objeto. Idempotente: apagar algo que não existe não é erro — quem chama está
    /// removendo a linha do banco, que é a fonte da verdade, e um blob já ausente é o estado
    /// desejado.
    /// </summary>
    Task DeleteAsync(string containerName, string blobPath, CancellationToken ct = default);
}
