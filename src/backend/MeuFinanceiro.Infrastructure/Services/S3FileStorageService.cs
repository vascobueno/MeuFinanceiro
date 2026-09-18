using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using MeuFinanceiro.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;

namespace MeuFinanceiro.Infrastructure.Services;

/// <summary>
/// Armazenamento de arquivo via protocolo S3 (MinIO em dev/produção, ou qualquer outro serviço
/// S3-compatible — Cloudflare R2, Backblaze B2, AWS S3 de verdade — trocando só `Storage:ServiceUrl`;
/// docs/00-visao-geral.md, docs/23-configuracao-local.md, docs/33-versions.md). Buckets são
/// privados por padrão (docs/12-seguranca.md); nenhuma URL pública é gerada aqui.
/// </summary>
public class S3FileStorageService : IFileStorageService
{
    private readonly AmazonS3Client _s3Client;

    public S3FileStorageService(IConfiguration configuration)
    {
        var serviceUrl = configuration["Storage:ServiceUrl"]
            ?? throw new InvalidOperationException("Configuração 'Storage:ServiceUrl' não configurada.");
        var accessKey = configuration["Storage:AccessKey"]
            ?? throw new InvalidOperationException("Configuração 'Storage:AccessKey' não configurada.");
        var secretKey = configuration["Storage:SecretKey"]
            ?? throw new InvalidOperationException("Configuração 'Storage:SecretKey' não configurada.");

        var config = new AmazonS3Config
        {
            ServiceURL = serviceUrl,
            // Path-style (http://host/bucket/key) em vez do virtual-hosted style padrão do SDK
            // (http://bucket.host/key) — MinIO (e a maioria dos S3-compatible self-hosted) só
            // aceita path-style; AWS S3 de verdade aceita os dois.
            ForcePathStyle = true,
        };
        var credentials = new BasicAWSCredentials(accessKey, secretKey);
        _s3Client = new AmazonS3Client(credentials, config);
    }

    public async Task<string> SaveAsync(string containerName, string fileName, Stream content, string contentType, CancellationToken ct = default)
    {
        if (!await AmazonS3Util.DoesS3BucketExistV2Async(_s3Client, containerName))
        {
            await _s3Client.PutBucketAsync(new PutBucketRequest { BucketName = containerName }, ct);
        }

        await _s3Client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = containerName,
            Key = fileName,
            InputStream = content,
            ContentType = contentType,
        }, ct);

        return fileName;
    }

    public async Task<Stream> OpenReadAsync(string containerName, string blobPath, CancellationToken ct = default)
    {
        var response = await _s3Client.GetObjectAsync(new GetObjectRequest
        {
            BucketName = containerName,
            Key = blobPath,
        }, ct);
        return response.ResponseStream;
    }

    public async Task DeleteAsync(string containerName, string blobPath, CancellationToken ct = default)
    {
        try
        {
            await _s3Client.DeleteObjectAsync(new DeleteObjectRequest
            {
                BucketName = containerName,
                Key = blobPath,
            }, ct);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Objeto (ou bucket) já não existe — é o estado desejado, não uma falha.
        }
    }
}
