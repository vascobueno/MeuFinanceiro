using System.Text.RegularExpressions;

namespace MeuFinanceiro.Application.Reconciliation.Import;

/// <summary>
/// O "04/06" de um item de fatura como número e total. Antes a conciliação fazia
/// <c>label.Contains(numero.ToString())</c>: a parcela 1 batia com "01/06", "10/12" e "04/10", e a
/// parcela 4 batia com "04/06" e "01/04" — qualquer dígito em qualquer posição.
/// </summary>
public static partial class InstallmentLabel
{
    public static (int Number, int Total)? TryParse(string? label)
    {
        if (string.IsNullOrWhiteSpace(label)) return null;
        var m = Shape().Match(label.Trim());
        if (!m.Success) return null;

        var number = int.Parse(m.Groups[1].Value);
        var total = int.Parse(m.Groups[2].Value);
        if (number < 1 || total < 1 || number > total) return null;
        return (number, total);
    }

    [GeneratedRegex(@"^(?:parcela\s+)?(\d{1,3})\s*[/-]\s*(\d{1,3})$", RegexOptions.IgnoreCase)]
    private static partial Regex Shape();
}
