using System.Security.Cryptography;
using ClosedXML.Excel;
using MeuFinanceiro.Application.Common.Exceptions;
using MeuFinanceiro.Application.Common.Interfaces;
using MeuFinanceiro.Application.Imports;
using MeuFinanceiro.Application.Merchants;
using MeuFinanceiro.Domain.Entities;
using MeuFinanceiro.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using static MeuFinanceiro.Application.Imports.ImportRowValidator;

namespace MeuFinanceiro.Infrastructure.Services;

/// <summary>
/// docs/16-importacao-inicial.md: Excel -> Upload -> Validação -> Prévia -> Importação ->
/// Relatório -> Conferência. Colunas esperadas na primeira planilha (linha 1 = cabeçalho):
/// Data | Descrição | Tipo | Conta | Cartão | Categoria | Valor | Parcela | Recorrência | Observação.
/// Regra 2 (novos lançamentos são feitos pelo sistema): a importação cria lançamentos já
/// liquidados (Status=Confirmed); não recria planos de parcelamento/recorrência "vivos" —
/// essas colunas viram apenas anotação no campo Observação do lançamento histórico.
/// </summary>
public class ExcelImportService : IExcelImportService
{
    private const string ContainerName = "imports";

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IFileStorageService _fileStorage;
    private readonly IMerchantService _merchantService;

    public ExcelImportService(IApplicationDbContext db, ICurrentUserService currentUser, IFileStorageService fileStorage, IMerchantService merchantService)
    {
        _db = db;
        _currentUser = currentUser;
        _fileStorage = fileStorage;
        _merchantService = merchantService;
    }

    public async Task<ImportPreviewResponse> UploadAndValidateAsync(Stream fileContent, string originalFileName, bool force, CancellationToken ct = default)
    {
        await using var buffer = new MemoryStream();
        await fileContent.CopyToAsync(buffer, ct);
        buffer.Position = 0;

        var hash = Convert.ToHexString(await SHA256.HashDataAsync(buffer, ct));
        buffer.Position = 0;

        var existing = await _db.ImportFiles
            .Where(f => f.GrupoId == _currentUser.GrupoId && f.FileHash == hash)
            .OrderByDescending(f => f.CreatedAt)
            .FirstOrDefaultAsync(ct);

        var alreadyImportedBefore = existing?.Status == ImportStatus.Imported;

        var (accountNames, cardNames, categoryNames) = await LoadLookupsAsync(ct);
        var rawRows = ParseWorkbook(buffer);
        var validatedRows = rawRows.Select(r => Validate(r, accountNames, cardNames, categoryNames)).ToList();
        var duplicateRowNumbers = await FindDuplicateRowNumbersAsync(validatedRows, ct);

        ImportFile? importFile = null;

        if (!alreadyImportedBefore || force)
        {
            buffer.Position = 0;
            var blobPath = await _fileStorage.SaveAsync(ContainerName, $"{_currentUser.GrupoId}/{Guid.NewGuid()}-{originalFileName}", buffer, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", ct);

            importFile = new ImportFile
            {
                GrupoId = _currentUser.GrupoId,
                OriginalFileName = originalFileName,
                FileHash = hash,
                BlobPath = blobPath,
                Status = ImportStatus.Validated,
                CreatedBy = _currentUser.UserId
            };
            _db.ImportFiles.Add(importFile);
            await _db.SaveChangesAsync(ct);
        }

        var previewRows = validatedRows.Select(r => ToPreviewRow(r, duplicateRowNumbers.Contains(r.RowNumber))).ToList();

        return new ImportPreviewResponse(
            importFile?.Id ?? Guid.Empty,
            originalFileName,
            alreadyImportedBefore,
            previewRows.Count,
            previewRows.Count(r => r.IsValid),
            previewRows.Count(r => !r.IsValid),
            previewRows.Count(r => r.IsPossibleDuplicate),
            previewRows
        );
    }

    public async Task<ConfirmImportResponse> ConfirmAsync(Guid importFileId, CancellationToken ct = default)
    {
        var importFile = await _db.ImportFiles.FirstOrDefaultAsync(f => f.Id == importFileId, ct)
            ?? throw new NotFoundException(nameof(ImportFile), importFileId);
        if (importFile.GrupoId != _currentUser.GrupoId) throw new ForbiddenException();

        if (importFile.Status == ImportStatus.Imported)
            throw new BusinessRuleException("arquivo-ja-importado", "Este arquivo já foi importado anteriormente.");

        if (importFile.BlobPath is null)
            throw new BusinessRuleException("arquivo-sem-conteudo", "Arquivo não possui conteúdo armazenado para reprocessar.");

        await using var stream = await _fileStorage.OpenReadAsync(ContainerName, importFile.BlobPath, ct);
        await using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, ct);
        buffer.Position = 0;

        var (accountNames, cardNames, categoryNames) = await LoadLookupsAsync(ct);
        var accountsById = await _db.Accounts.Where(a => a.GrupoId == _currentUser.GrupoId).ToDictionaryAsync(a => a.Name, a => a.Id, StringComparer.OrdinalIgnoreCase, ct);
        var cardsById = await _db.CreditCards.Where(c => c.GrupoId == _currentUser.GrupoId).ToDictionaryAsync(c => c.Name, c => c.Id, StringComparer.OrdinalIgnoreCase, ct);
        var categoriesById = await _db.Categories.Where(c => c.GrupoId == _currentUser.GrupoId).ToDictionaryAsync(c => c.Name, c => c.Id, StringComparer.OrdinalIgnoreCase, ct);

        var rawRows = ParseWorkbook(buffer);
        var validatedRows = rawRows.Select(r => Validate(r, accountNames, cardNames, categoryNames)).ToList();

        var rowsImported = 0;
        var rowsRejected = 0;

        foreach (var row in validatedRows)
        {
            if (!row.IsValid || row.Date is null || row.Amount is null || row.Type is null || row.Description is null)
            {
                rowsRejected++;
                continue;
            }

            var merchantId = await _merchantService.GetOrCreateAndTouchAsync(row.Description, ct);

            var notesParts = new List<string>();
            if (!string.IsNullOrWhiteSpace(row.InstallmentRaw)) notesParts.Add($"Parcela original: {row.InstallmentRaw}");
            if (!string.IsNullOrWhiteSpace(row.RecurrenceRaw)) notesParts.Add($"Recorrência original: {row.RecurrenceRaw}");
            if (!string.IsNullOrWhiteSpace(row.Notes)) notesParts.Add(row.Notes);
            notesParts.Add("Importado da migração inicial (Excel).");

            var transaction = new FinancialTransaction
            {
                GrupoId = _currentUser.GrupoId,
                TransactionDate = row.Date.Value,
                Description = row.Description,
                MerchantId = merchantId,
                CategoryId = row.CategoryRaw is not null && categoriesById.TryGetValue(row.CategoryRaw, out var categoryId) ? categoryId : null,
                AccountId = row.IsAccount && row.AccountOrCardRaw is not null && accountsById.TryGetValue(row.AccountOrCardRaw, out var accountId) ? accountId : null,
                CreditCardId = !row.IsAccount && row.AccountOrCardRaw is not null && cardsById.TryGetValue(row.AccountOrCardRaw, out var cardId) ? cardId : null,
                Type = row.Type.Value,
                Direction = row.Type == TransactionType.Income ? TransactionDirection.Credit : TransactionDirection.Debit,
                Amount = row.Amount.Value,
                Status = TransactionStatus.Confirmed,
                Notes = string.Join(" | ", notesParts),
                CreatedBy = _currentUser.UserId
            };

            if (transaction.AccountId is null && transaction.CreditCardId is null)
            {
                rowsRejected++;
                continue;
            }

            _db.FinancialTransactions.Add(transaction);
            rowsImported++;
        }

        var summary = $"{rowsImported} lançamento(s) importado(s), {rowsRejected} rejeitado(s) de {validatedRows.Count} linha(s) processada(s).";

        importFile.Status = ImportStatus.Imported;
        importFile.RowsImported = rowsImported;
        importFile.RowsRejected = rowsRejected;
        importFile.ResultSummary = summary;
        importFile.UpdatedAt = DateTime.UtcNow;
        importFile.UpdatedBy = _currentUser.UserId;

        await _db.SaveChangesAsync(ct);

        return new ConfirmImportResponse(importFile.Id, rowsImported, rowsRejected, summary);
    }

    public async Task<IReadOnlyList<ImportFileResponse>> GetHistoryAsync(CancellationToken ct = default)
    {
        var files = await _db.ImportFiles
            .AsNoTracking()
            .Where(f => f.GrupoId == _currentUser.GrupoId)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync(ct);

        return files.Select(f => new ImportFileResponse(f.Id, f.OriginalFileName, f.Status.ToString(), f.CreatedAt, f.RowsImported, f.RowsRejected, f.ResultSummary)).ToList();
    }

    // ----------------- helpers -----------------

    private async Task<(HashSet<string> accounts, HashSet<string> cards, HashSet<string> categories)> LoadLookupsAsync(CancellationToken ct)
    {
        var accountNames = await _db.Accounts.Where(a => a.GrupoId == _currentUser.GrupoId).Select(a => a.Name).ToListAsync(ct);
        var cardNames = await _db.CreditCards.Where(c => c.GrupoId == _currentUser.GrupoId).Select(c => c.Name).ToListAsync(ct);
        var categoryNames = await _db.Categories.Where(c => c.GrupoId == _currentUser.GrupoId).Select(c => c.Name).ToListAsync(ct);

        return (
            new HashSet<string>(accountNames, StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(cardNames, StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(categoryNames, StringComparer.OrdinalIgnoreCase)
        );
    }

    private async Task<HashSet<int>> FindDuplicateRowNumbersAsync(List<ValidatedRow> rows, CancellationToken ct)
    {
        var candidateDates = rows.Where(r => r.Date is not null).Select(r => r.Date!.Value).Distinct().ToList();
        if (candidateDates.Count == 0) return new HashSet<int>();

        var existing = await _db.FinancialTransactions
            .Where(t => t.GrupoId == _currentUser.GrupoId && candidateDates.Contains(t.TransactionDate))
            .Select(t => new { t.TransactionDate, t.Description, t.Amount })
            .ToListAsync(ct);

        var existingSet = existing
            .Select(t => (t.TransactionDate, Description: t.Description.Trim().ToUpperInvariant(), t.Amount))
            .ToHashSet();

        var duplicateRowNumbers = new HashSet<int>();
        foreach (var row in rows)
        {
            if (row.Date is null || row.Amount is null || row.Description is null) continue;

            var key = (row.Date.Value, Description: row.Description.Trim().ToUpperInvariant(), row.Amount.Value);
            if (existingSet.Contains(key)) duplicateRowNumbers.Add(row.RowNumber);
        }

        return duplicateRowNumbers;
    }

    /// <summary>Lê a primeira planilha a partir da linha 2 (linha 1 = cabeçalho), parando na primeira linha totalmente vazia.</summary>
    private static List<RawRow> ParseWorkbook(Stream stream)
    {
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheets.First();

        var headerRow = worksheet.Row(1);
        var columnIndexByHeader = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var cell in headerRow.CellsUsed())
        {
            var header = cell.GetString().Trim();
            if (header.Length > 0) columnIndexByHeader[header] = cell.Address.ColumnNumber;
        }

        int ColumnOf(params string[] names) => names.Select(n => columnIndexByHeader.GetValueOrDefault(n, -1)).FirstOrDefault(i => i >= 0, -1);

        var dataCol = ColumnOf("Data");
        var descricaoCol = ColumnOf("Descrição", "Descricao");
        var tipoCol = ColumnOf("Tipo");
        var contaCol = ColumnOf("Conta");
        var cartaoCol = ColumnOf("Cartão", "Cartao");
        var categoriaCol = ColumnOf("Categoria");
        var valorCol = ColumnOf("Valor");
        var parcelaCol = ColumnOf("Parcela");
        var recorrenciaCol = ColumnOf("Recorrência", "Recorrencia");
        var observacaoCol = ColumnOf("Observação", "Observacao");

        var rows = new List<RawRow>();
        var rowNumber = 2;

        while (true)
        {
            var row = worksheet.Row(rowNumber);
            if (row.IsEmpty()) break;

            string? Get(int col) => col < 0 ? null : row.Cell(col).GetString();

            var conta = Get(contaCol);
            var cartao = Get(cartaoCol);

            rows.Add(new RawRow(
                rowNumber,
                Get(dataCol),
                Get(descricaoCol),
                Get(tipoCol),
                !string.IsNullOrWhiteSpace(conta) ? conta : cartao,
                Get(categoriaCol),
                Get(valorCol),
                Get(parcelaCol),
                Get(recorrenciaCol),
                Get(observacaoCol)
            ));

            rowNumber++;
        }

        return rows;
    }

    private static ImportRowPreview ToPreviewRow(ValidatedRow row, bool isDuplicate) => new(
        row.RowNumber, row.Date, row.Description, row.Type?.ToString(), row.AccountOrCardRaw, row.CategoryRaw,
        row.Amount, row.InstallmentRaw, row.RecurrenceRaw, row.Notes, row.IsValid, isDuplicate, row.Errors
    );
}
