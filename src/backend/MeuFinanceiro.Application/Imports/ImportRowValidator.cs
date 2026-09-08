using System.Globalization;
using MeuFinanceiro.Domain.Enums;

namespace MeuFinanceiro.Application.Imports;

/// <summary>
/// Validação pura de uma linha da planilha (sem I/O), para ser testável isoladamente
/// (docs/20-testes.md) e reutilizável entre prévia e confirmação. Valida datas, valores,
/// contas/cartões e categorias (docs/16-importacao-inicial.md).
/// </summary>
public static class ImportRowValidator
{
    public record RawRow(
        int RowNumber,
        string? DateRaw,
        string? Description,
        string? TypeRaw,
        string? AccountOrCardRaw,
        string? CategoryRaw,
        string? AmountRaw,
        string? InstallmentRaw,
        string? RecurrenceRaw,
        string? Notes
    );

    public record ValidatedRow(
        int RowNumber,
        DateOnly? Date,
        string? Description,
        TransactionType? Type,
        string? AccountOrCardRaw,
        bool IsAccount,
        string? CategoryRaw,
        decimal? Amount,
        string? InstallmentRaw,
        string? RecurrenceRaw,
        string? Notes,
        bool IsValid,
        IReadOnlyList<string> Errors
    );

    private static readonly string[] DateFormats = { "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd" };

    public static ValidatedRow Validate(
        RawRow row,
        IReadOnlySet<string> accountNames,
        IReadOnlySet<string> cardNames,
        IReadOnlySet<string> categoryNames)
    {
        var errors = new List<string>();

        var date = ParseDate(row.DateRaw);
        if (date is null) errors.Add("Data inválida ou ausente.");

        if (string.IsNullOrWhiteSpace(row.Description)) errors.Add("Descrição ausente.");

        var type = ParseType(row.TypeRaw);
        if (type is null) errors.Add("Tipo inválido — use 'Receita' ou 'Despesa'.");

        var amount = ParseAmount(row.AmountRaw);
        if (amount is null) errors.Add("Valor inválido ou ausente.");
        else if (amount == 0) errors.Add("Valor não pode ser zero.");

        var isAccount = false;
        var normalizedOrigin = row.AccountOrCardRaw?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedOrigin))
        {
            errors.Add("Conta ou cartão de origem ausente.");
        }
        else if (accountNames.Contains(normalizedOrigin, StringComparer.OrdinalIgnoreCase))
        {
            isAccount = true;
        }
        else if (!cardNames.Contains(normalizedOrigin, StringComparer.OrdinalIgnoreCase))
        {
            errors.Add($"Conta/cartão '{normalizedOrigin}' não encontrado(a). Cadastre-o(a) antes de importar ou corrija o nome na planilha.");
        }

        if (!string.IsNullOrWhiteSpace(row.CategoryRaw) && !categoryNames.Contains(row.CategoryRaw.Trim(), StringComparer.OrdinalIgnoreCase))
            errors.Add($"Categoria '{row.CategoryRaw}' não encontrada. Cadastre-a antes de importar ou deixe em branco.");

        return new ValidatedRow(
            row.RowNumber, date, row.Description?.Trim(), type, normalizedOrigin, isAccount,
            row.CategoryRaw?.Trim(), amount, row.InstallmentRaw, row.RecurrenceRaw, row.Notes,
            errors.Count == 0, errors);
    }

    private static DateOnly? ParseDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        if (DateOnly.TryParseExact(raw.Trim(), DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var exact))
            return exact;

        return DateOnly.TryParse(raw.Trim(), CultureInfo.GetCultureInfo("pt-BR"), DateTimeStyles.None, out var parsed) ? parsed : null;
    }

    private static TransactionType? ParseType(string? raw)
    {
        var normalized = raw?.Trim().ToUpperInvariant();
        return normalized switch
        {
            "RECEITA" or "INCOME" => TransactionType.Income,
            "DESPESA" or "EXPENSE" => TransactionType.Expense,
            _ => null
        };
    }

    private static decimal? ParseAmount(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var normalized = raw.Trim().Replace("R$", "", StringComparison.OrdinalIgnoreCase).Trim();

        // Aceita tanto "1.234,56" (pt-BR) quanto "1234.56" (invariante).
        if (normalized.Contains(',') && normalized.Contains('.'))
            normalized = normalized.Replace(".", "").Replace(",", ".");
        else if (normalized.Contains(','))
            normalized = normalized.Replace(",", ".");

        return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) ? amount : null;
    }
}
