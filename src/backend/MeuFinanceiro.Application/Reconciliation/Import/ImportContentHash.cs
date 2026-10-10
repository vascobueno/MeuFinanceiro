using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace MeuFinanceiro.Application.Reconciliation.Import;

/// <summary>
/// Impressão digital do conteúdo de um extrato ou fatura, para recusar a segunda importação do
/// mesmo documento. Usa o conteúdo normalizado — e não o nome do arquivo, que muda, nem o
/// arquivo, que na colagem nem existe.
///
/// A descrição entra em maiúsculas e com espaços colapsados: colar o mesmo trecho com uma
/// quebra de linha diferente não pode gerar outra impressão.
/// </summary>
public static partial class ImportContentHash
{
    public static string ForStatement(ImportBankStatementRequest r) => Hash(
        "extrato",
        r.AccountId.ToString(),
        Date(r.ReferenceStart), Date(r.ReferenceEnd),
        Money(r.OpeningBalance), Money(r.ClosingBalance),
        string.Join("\n", r.Items.Select(i => $"{Date(i.Date)};{Money(i.Amount)};{Normalize(i.Description)}")));

    public static string ForInvoice(ImportCardInvoiceRequest r) => Hash(
        "fatura",
        r.CreditCardId.ToString(),
        Date(r.ClosingDate), Date(r.DueDate),
        Money(r.TotalAmount),
        string.Join("\n", r.Items.Select(i => $"{Date(i.Date)};{Money(i.Amount)};{i.InstallmentLabel};{Normalize(i.Description)}")));

    private static string Hash(params string[] parts)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("|", parts)));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string Date(DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string Money(decimal v) => v.ToString("0.00", CultureInfo.InvariantCulture);

    private static string Normalize(string s) => Spaces().Replace(s.Trim().ToUpperInvariant(), " ");

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}
