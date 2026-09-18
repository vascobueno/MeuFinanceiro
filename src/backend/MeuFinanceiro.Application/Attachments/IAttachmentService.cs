namespace MeuFinanceiro.Application.Attachments;

/// <summary>Anexos de lançamento e de parcelamento (docs/06, docs/07, docs/12).</summary>
public interface IAttachmentService
{
    Task<AttachmentResponse> UploadAsync(AttachmentOwner owner, Stream content, string fileName, CancellationToken ct = default);

    Task<IReadOnlyList<AttachmentResponse>> GetForOwnerAsync(AttachmentOwner owner, CancellationToken ct = default);

    Task<AttachmentDownload> DownloadAsync(Guid attachmentId, CancellationToken ct = default);

    Task DeleteAsync(Guid attachmentId, CancellationToken ct = default);

    /// <summary>
    /// Remove os anexos de um dono que está prestes a ser excluído. Chamado ANTES de excluir o
    /// dono: o cascade do banco apagaria as linhas, mas não os objetos no storage.
    /// </summary>
    Task DeleteForOwnerAsync(AttachmentOwner owner, CancellationToken ct = default);
}
