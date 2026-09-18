using System.Security.Cryptography;
using MeuFinanceiro.Application.Common.Exceptions;
using MeuFinanceiro.Application.Common.Interfaces;
using MeuFinanceiro.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MeuFinanceiro.Application.Attachments;

/// <summary>
/// Anexos de lançamento e de parcelamento. Toda a validação sensível mora aqui — um lugar só —
/// em vez de espalhada pelos controllers dos donos.
///
/// Ponto central de segurança: o projeto NÃO tem query filter global por grupo (nenhum
/// HasQueryFilter no AppDbContext), então cada consulta abaixo filtra GrupoId explicitamente.
/// Sem isso, um GUID adivinhado bastaria para baixar o documento de outro grupo.
/// </summary>
public class AttachmentService : IAttachmentService
{
    /// <summary>Mesmo teto do upload de planilha (ImportsController).</summary>
    public const long MaxFileSizeBytes = 10 * 1024 * 1024;

    /// <summary>Teto por dono — evita que um lançamento vire depósito de arquivos.</summary>
    public const int MaxAttachmentsPerOwner = 10;

    private const string ContainerName = "attachments";

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IFileStorageService _fileStorage;

    public AttachmentService(IApplicationDbContext db, ICurrentUserService currentUser, IFileStorageService fileStorage)
    {
        _db = db;
        _currentUser = currentUser;
        _fileStorage = fileStorage;
    }

    public async Task<AttachmentResponse> UploadAsync(AttachmentOwner owner, Stream content, string fileName, CancellationToken ct = default)
    {
        if (!_currentUser.CanWrite)
            throw new ForbiddenException("Perfil somente leitura não pode anexar arquivos.");

        await EnsureOwnerExistsAsync(owner, ct);

        var existing = await OwnerQuery(owner).CountAsync(ct);
        if (existing >= MaxAttachmentsPerOwner)
            throw new BusinessRuleException("anexo-limite-excedido", $"Limite de {MaxAttachmentsPerOwner} anexos por item atingido.");

        var buffer = await ReadWithLimitAsync(content, ct);

        // A assinatura manda: extensão e Content-Type do multipart são escolhidos pelo cliente.
        var contentType = FileSignatureValidator.Resolve(buffer.AsSpan(), fileName, out var error)
            ?? throw new BusinessRuleException("anexo-tipo-invalido", error!);

        var attachmentId = Guid.NewGuid();
        // O nome original NÃO entra na chave: é entrada não confiável. Fica só na coluna.
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var blobPath = $"{_currentUser.GrupoId}/{attachmentId}{extension}";

        using var stream = new MemoryStream(buffer, writable: false);
        await _fileStorage.SaveAsync(ContainerName, blobPath, stream, contentType, ct);

        var attachment = new Attachment
        {
            Id = attachmentId,
            GrupoId = _currentUser.GrupoId,
            FileName = Path.GetFileName(fileName),
            ContentType = contentType,
            SizeBytes = buffer.Length,
            FileHash = Convert.ToHexString(SHA256.HashData(buffer)).ToLowerInvariant(),
            BlobPath = blobPath,
            FinancialTransactionId = owner.Kind == AttachmentOwnerKind.Transaction ? owner.Id : null,
            InstallmentPlanId = owner.Kind == AttachmentOwnerKind.InstallmentPlan ? owner.Id : null,
        };

        _db.Attachments.Add(attachment);
        await _db.SaveChangesAsync(ct);

        return ToResponse(attachment);
    }

    public async Task<IReadOnlyList<AttachmentResponse>> GetForOwnerAsync(AttachmentOwner owner, CancellationToken ct = default)
    {
        await EnsureOwnerExistsAsync(owner, ct);

        return await OwnerQuery(owner)
            .AsNoTracking()
            .OrderBy(a => a.CreatedAt)
            .Select(a => new AttachmentResponse(a.Id, a.FileName, a.ContentType, a.SizeBytes, a.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<AttachmentDownload> DownloadAsync(Guid attachmentId, CancellationToken ct = default)
    {
        var attachment = await FindOwnedAsync(attachmentId, ct);
        var content = await _fileStorage.OpenReadAsync(ContainerName, attachment.BlobPath, ct);
        return new AttachmentDownload(content, attachment.ContentType, attachment.FileName);
    }

    public async Task DeleteAsync(Guid attachmentId, CancellationToken ct = default)
    {
        if (!_currentUser.CanWrite)
            throw new ForbiddenException("Perfil somente leitura não pode remover anexos.");

        var attachment = await FindOwnedAsync(attachmentId, ct);

        _db.Attachments.Remove(attachment);
        await _db.SaveChangesAsync(ct);

        // Depois de a linha sair: se o blob falhar, sobra um objeto órfão que ninguém referencia —
        // incômodo de espaço, não de correção. Na ordem inversa, uma falha no banco deixaria uma
        // linha apontando para um blob que já não existe, e aí o download quebraria.
        await _fileStorage.DeleteAsync(ContainerName, attachment.BlobPath, ct);
    }

    public async Task DeleteForOwnerAsync(AttachmentOwner owner, CancellationToken ct = default)
    {
        var attachments = await OwnerQuery(owner).ToListAsync(ct);
        if (attachments.Count == 0) return;

        _db.Attachments.RemoveRange(attachments);
        await _db.SaveChangesAsync(ct);

        foreach (var attachment in attachments)
            await _fileStorage.DeleteAsync(ContainerName, attachment.BlobPath, ct);
    }

    // ---------------- apoio ----------------

    private IQueryable<Attachment> OwnerQuery(AttachmentOwner owner) =>
        _db.Attachments.Where(a => a.GrupoId == _currentUser.GrupoId
            && (owner.Kind == AttachmentOwnerKind.Transaction
                ? a.FinancialTransactionId == owner.Id
                : a.InstallmentPlanId == owner.Id));

    /// <summary>
    /// Busca o anexo restrito ao grupo do usuário. 404 (não 403) quando é de outro grupo: um 403
    /// confirmaria que aquele id existe.
    /// </summary>
    private async Task<Attachment> FindOwnedAsync(Guid attachmentId, CancellationToken ct) =>
        await _db.Attachments.FirstOrDefaultAsync(
            a => a.Id == attachmentId && a.GrupoId == _currentUser.GrupoId, ct)
        ?? throw new NotFoundException("Anexo", attachmentId);

    private async Task EnsureOwnerExistsAsync(AttachmentOwner owner, CancellationToken ct)
    {
        var exists = owner.Kind switch
        {
            AttachmentOwnerKind.Transaction => await _db.FinancialTransactions
                .AnyAsync(t => t.Id == owner.Id && t.GrupoId == _currentUser.GrupoId, ct),
            _ => await _db.InstallmentPlans
                .AnyAsync(p => p.Id == owner.Id && p.GrupoId == _currentUser.GrupoId, ct),
        };

        if (!exists)
            throw new NotFoundException(
                owner.Kind == AttachmentOwnerKind.Transaction ? "Lançamento" : "Parcelamento", owner.Id);
    }

    /// <summary>
    /// Lê o stream inteiro com teto. O conteúdo precisa estar em memória de qualquer forma — para
    /// conferir a assinatura, calcular o hash e enviar ao storage —, e 10 MB é um teto seguro.
    /// </summary>
    private static async Task<byte[]> ReadWithLimitAsync(Stream content, CancellationToken ct)
    {
        using var memory = new MemoryStream();
        var chunk = new byte[81920];
        int read;

        while ((read = await content.ReadAsync(chunk, ct)) > 0)
        {
            if (memory.Length + read > MaxFileSizeBytes)
                throw new BusinessRuleException("anexo-tamanho-excedido", $"Arquivo acima do limite de {MaxFileSizeBytes / (1024 * 1024)} MB.");
            memory.Write(chunk, 0, read);
        }

        if (memory.Length == 0)
            throw new BusinessRuleException("anexo-vazio", "Arquivo vazio.");

        return memory.ToArray();
    }

    private static AttachmentResponse ToResponse(Attachment a) =>
        new(a.Id, a.FileName, a.ContentType, a.SizeBytes, a.CreatedAt);
}
