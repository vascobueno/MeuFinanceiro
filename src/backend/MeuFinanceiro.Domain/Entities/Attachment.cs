using MeuFinanceiro.Domain.Common;

namespace MeuFinanceiro.Domain.Entities;

/// <summary>
/// Arquivo anexado a um lançamento ou a um parcelamento — nota fiscal, boleto, comprovante.
/// O conteúdo vive no storage S3-compatible (<see cref="BlobPath"/>); aqui ficam só os metadados.
///
/// Uma única tabela com dois FKs anuláveis, em vez de uma tabela por tipo de dono: é o mesmo
/// idioma que <see cref="FinancialTransaction"/> já usa para a origem ("exatamente uma entre
/// AccountId e CreditCardId"), permite que um service/controller/componente cubra os dois casos,
/// e aceitar um terceiro dono depois custa uma coluna em vez de uma tabela nova.
///
/// O anexo do parcelamento fica no plano inteiro, não na parcela: a nota da compra é um documento
/// só e vale para todas as parcelas dela.
/// </summary>
public class Attachment : GrupoOwnedEntity
{
    /// <summary>Nome original enviado pelo usuário. Só para exibir e para o Content-Disposition —
    /// nunca entra na chave do storage, porque é entrada não confiável.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>MIME deduzido da assinatura do arquivo no servidor, nunca o informado pelo cliente.</summary>
    public string ContentType { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    /// <summary>SHA-256 do conteúdo — integridade e detecção de duplicata (segue <see cref="ImportFile.FileHash"/>).</summary>
    public string FileHash { get; set; } = string.Empty;

    /// <summary>Chave do objeto no storage: {grupoId}/{attachmentId}{extensão}.</summary>
    public string BlobPath { get; set; } = string.Empty;

    // Dono: exatamente um dos dois preenchido (validado na Application).
    public Guid? FinancialTransactionId { get; set; }
    public FinancialTransaction? FinancialTransaction { get; set; }

    public Guid? InstallmentPlanId { get; set; }
    public InstallmentPlan? InstallmentPlan { get; set; }
}
