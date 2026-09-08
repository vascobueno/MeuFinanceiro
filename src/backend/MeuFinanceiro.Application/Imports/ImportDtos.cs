namespace MeuFinanceiro.Application.Imports;

/// <summary>
/// Uma linha da planilha, já validada. Colunas esperadas (docs/06-lancamentos.md):
/// Data | Descrição | Tipo | Conta | Cartão | Categoria | Valor | Parcela | Recorrência | Observação.
/// Parcela/Recorrência aqui são apenas informativas do histórico — regra 2 (novos lançamentos
/// são feitos pelo sistema) significa que o Excel não recria planos de parcelamento/recorrência
/// vivos, apenas lançamentos já liquidados, com a informação original preservada em Notes.
/// </summary>
public record ImportRowPreview(
    int RowNumber,
    DateOnly? Date,
    string? Description,
    string? TypeRaw,
    string? AccountOrCardRaw,
    string? CategoryRaw,
    decimal? Amount,
    string? InstallmentRaw,
    string? RecurrenceRaw,
    string? Notes,
    bool IsValid,
    bool IsPossibleDuplicate,
    IReadOnlyList<string> Errors
);

public record ImportPreviewResponse(
    Guid ImportFileId,
    string OriginalFileName,
    bool AlreadyImportedBefore,
    int RowsTotal,
    int RowsValid,
    int RowsInvalid,
    int RowsPossibleDuplicate,
    IReadOnlyList<ImportRowPreview> Rows
);

public record ConfirmImportResponse(Guid ImportFileId, int RowsImported, int RowsRejected, string ResultSummary);

public record ImportFileResponse(
    Guid Id,
    string OriginalFileName,
    string Status,
    DateTime CreatedAt,
    int? RowsImported,
    int? RowsRejected,
    string? ResultSummary
);
