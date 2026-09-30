namespace MeuFinanceiro.Application.Attachments;

/// <summary>Tipo de entidade que possui o anexo. Ver <see cref="Domain.Entities.Attachment"/>.</summary>
public enum AttachmentOwnerKind
{
    Transaction = 1,
    InstallmentPlan = 2,
}

/// <summary>Dono do anexo — o par (tipo, id) que o identifica.</summary>
public record AttachmentOwner(AttachmentOwnerKind Kind, Guid Id);

public record AttachmentResponse(
    Guid Id,
    string FileName,
    string ContentType,
    long SizeBytes,
    DateTime CreatedAt
);

/// <summary>Conteúdo para download, já com os metadados que o controller precisa devolver.</summary>
public record AttachmentDownload(Stream Content, string ContentType, string FileName);
