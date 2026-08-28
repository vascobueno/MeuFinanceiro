namespace MeuFinanceiro.Application.Imports;

/// <summary>
/// docs/16-importacao-inicial.md: Excel -> Upload -> Validação -> Prévia -> Importação ->
/// Relatório -> Conferência. Guarda arquivo original + hash + usuário + data/hora + resultado.
/// </summary>
public interface IExcelImportService
{
    /// <summary>
    /// Faz upload, calcula hash e valida a planilha, retornando uma prévia linha a linha sem
    /// criar nenhum lançamento ainda. Se o mesmo arquivo (mesmo hash) já foi importado antes
    /// para este usuário e <paramref name="force"/> for false, sinaliza isso na resposta em vez
    /// de bloquear silenciosamente (regra: não reimportar sem confirmação explícita).
    /// </summary>
    Task<ImportPreviewResponse> UploadAndValidateAsync(Stream fileContent, string originalFileName, bool force, CancellationToken ct = default);

    /// <summary>Reprocessa o arquivo já validado e efetivamente cria os lançamentos das linhas válidas.</summary>
    Task<ConfirmImportResponse> ConfirmAsync(Guid importFileId, CancellationToken ct = default);

    Task<IReadOnlyList<ImportFileResponse>> GetHistoryAsync(CancellationToken ct = default);
}
