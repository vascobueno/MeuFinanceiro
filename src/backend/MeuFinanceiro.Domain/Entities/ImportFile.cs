using MeuFinanceiro.Domain.Common;
using MeuFinanceiro.Domain.Enums;

namespace MeuFinanceiro.Domain.Entities;

/// <summary>docs/16-importacao-inicial.md — migração inicial do Excel (regra 1: somente migração inicial).</summary>
public class ImportFile : HouseholdOwnedEntity
{
    public string OriginalFileName { get; set; } = string.Empty;
    public string FileHash { get; set; } = string.Empty;
    /// <summary>Caminho do arquivo original no Blob Storage (docs/00, docs/12 — blob privado).</summary>
    public string? BlobPath { get; set; }
    public ImportStatus Status { get; set; } = ImportStatus.Uploaded;
    public string? ResultSummary { get; set; }
    public int? RowsImported { get; set; }
    public int? RowsRejected { get; set; }
}
