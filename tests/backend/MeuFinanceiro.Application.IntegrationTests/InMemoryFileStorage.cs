using System.Collections.Concurrent;
using MeuFinanceiro.Application.Common.Interfaces;

namespace MeuFinanceiro.Application.IntegrationTests;

/// <summary>
/// Storage em memória no lugar do MinIO.
///
/// Existe porque o CI (.github/workflows/ci.yml) não sobe serviço nenhum — sem isto, todo teste
/// de anexo dependeria de um MinIO local e quebraria no pipeline. Em troca, estes testes cobrem
/// a API, a autorização e o banco, mas NÃO o ida-e-volta real com o S3; essa parte é conferida à
/// mão contra o MinIO do docker-compose.
///
/// Guarda os bytes para que o teste de download possa comparar o conteúdo, e expõe
/// <see cref="Exists"/> para verificar que o DELETE realmente removeu o objeto — e não só a
/// linha do banco.
/// </summary>
public class InMemoryFileStorage : IFileStorageService
{
    private readonly ConcurrentDictionary<string, byte[]> _objects = new();

    private static string Key(string container, string path) => $"{container}/{path}";

    public int Count => _objects.Count;

    public bool Exists(string container, string path) => _objects.ContainsKey(Key(container, path));

    public Task<string> SaveAsync(string containerName, string fileName, Stream content, string contentType, CancellationToken ct = default)
    {
        using var memory = new MemoryStream();
        content.CopyTo(memory);
        _objects[Key(containerName, fileName)] = memory.ToArray();
        return Task.FromResult(fileName);
    }

    public Task<Stream> OpenReadAsync(string containerName, string blobPath, CancellationToken ct = default)
    {
        if (!_objects.TryGetValue(Key(containerName, blobPath), out var bytes))
            throw new FileNotFoundException(blobPath);

        return Task.FromResult<Stream>(new MemoryStream(bytes));
    }

    public Task DeleteAsync(string containerName, string blobPath, CancellationToken ct = default)
    {
        _objects.TryRemove(Key(containerName, blobPath), out _);
        return Task.CompletedTask;
    }
}
