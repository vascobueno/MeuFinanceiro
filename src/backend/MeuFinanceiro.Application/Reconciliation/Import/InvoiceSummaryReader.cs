using System.Text.RegularExpressions;

namespace MeuFinanceiro.Application.Reconciliation.Import;

/// <summary>O que o resumo da fatura diz: total, saldo anterior e datas.</summary>
public record InvoiceSummary(decimal? Total, decimal? PreviousBalance, DateOnly? Due, DateOnly? Closing, DateOnly? NextClosing);

/// <summary>
/// Lê o resumo da fatura no texto inteiro (colado ou extraído do PDF), nos formatos dos dois
/// emissores vistos (docs/08). Os rótulos e os valores às vezes estão em linhas diferentes, por isso
/// a leitura é sobre o texto todo, não linha a linha.
///
/// Santander: "Saldo Anterior 10.112,28", "(=) Saldo Desta Fatura 9.195,78" ou "Total desta Fatura R$"
/// com o valor na linha seguinte, "Vencimento" com a data na seguinte, e o histórico com "Esta
/// Fatura 04/07/26 a 03/08/26" (fecha no fim do período) e "Fatura Aberta 04/08/26 a 02/09/26"
/// (o próximo fechamento).
///
/// Pan: "Total da fatura (agosto) R$ 54,00", "Valor da fatura anterior (julho) R$ 54,00",
/// "… Vencimento" com a data no fim da linha seguinte, "fechamento desta fatura (05/08/2026)" e,
/// no bloco "Próxima fatura", "Fechamento: 03/09" — que é da próxima, não desta.
///
/// Cuidado deliberado: "Total a Pagar R$ 13.170,50" do Santander é a simulação do PARCELAMENTO
/// da fatura, não o total — não é lido.
/// </summary>
public static partial class InvoiceSummaryReader
{
    public static InvoiceSummary Read(string? text, DateOnly reference)
    {
        var t = (text ?? string.Empty).Replace("\r", string.Empty);

        decimal? total = Money(SantanderBalance().Match(t)) ?? Money(SantanderTotal().Match(t)) ?? Money(PanTotal().Match(t));
        decimal? previous = Money(SantanderPrevious().Match(t)) ?? Money(PanPrevious().Match(t));

        DateOnly? due = Date(SantanderDue().Match(t), reference, forward: true) ?? Date(PanDue().Match(t), reference, forward: true);
        DateOnly? closing = Date(SantanderPeriod().Match(t), reference, forward: false) ?? Date(PanClosing().Match(t), reference, forward: false);
        DateOnly? next = Date(SantanderOpenPeriod().Match(t), reference, forward: true) ?? Date(PanNextClosing().Match(t), reference, forward: true);

        return new InvoiceSummary(total, previous, due, closing, next);
    }

    private static decimal? Money(Match m) =>
        m.Success && ImportValueParser.TryParseMoney(m.Groups["v"].Value, out var v) ? v : null;

    private static DateOnly? Date(Match m, DateOnly reference, bool forward)
    {
        if (!m.Success) return null;
        var raw = m.Groups["d"].Value;
        if (ImportValueParser.TryParseDate(raw, reference, out var full) && raw.Length > 5) return full;
        // dd/MM: o vencimento e o próximo fechamento vêm depois da referência (podem virar o ano).
        var parts = raw.Split('/');
        if (!int.TryParse(parts[0], out var day) || !int.TryParse(parts[1], out var month) || month is < 1 or > 12) return null;
        foreach (var year in forward ? new[] { reference.Year, reference.Year + 1 } : new[] { reference.Year, reference.Year - 1 })
        {
            if (day > DateTime.DaysInMonth(year, month)) continue;
            var candidate = new DateOnly(year, month, day);
            if (forward ? candidate >= reference.AddDays(-3) : candidate <= reference.AddDays(45)) return candidate;
        }
        return null;
    }

    private const string Value = @"(?<v>-?\d{1,3}(?:\.\d{3})*,\d{2})";
    private const string DateToken = @"(?<d>\d{2}/\d{2}(?:/\d{2,4})?)";

    [GeneratedRegex(@"\(=\)\s*Saldo\s+Desta\s+Fatura\s+(?:R\$\s*)?" + Value, RegexOptions.IgnoreCase)]
    private static partial Regex SantanderBalance();

    [GeneratedRegex(@"Total\s+desta\s+Fatura\s+R\$\s*" + Value, RegexOptions.IgnoreCase)]
    private static partial Regex SantanderTotal();

    [GeneratedRegex(@"^Saldo\s+Anterior\s+(?:R\$\s*)?" + Value, RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex SantanderPrevious();

    [GeneratedRegex(@"^Vencimento\s*\n\s*" + DateToken + @"\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex SantanderDue();

    [GeneratedRegex(@"Esta\s+Fatura\s+\d{2}/\d{2}/\d{2,4}\s+a\s+" + DateToken, RegexOptions.IgnoreCase)]
    private static partial Regex SantanderPeriod();

    [GeneratedRegex(@"Fatura\s+Aberta\s+\d{2}/\d{2}/\d{2,4}\s+a\s+" + DateToken, RegexOptions.IgnoreCase)]
    private static partial Regex SantanderOpenPeriod();

    [GeneratedRegex(@"Total\s+da\s+fatura\s*\n?\s*\([^)\n]*\)\s*\n?\s*R\$\s*" + Value, RegexOptions.IgnoreCase)]
    private static partial Regex PanTotal();

    [GeneratedRegex(@"Valor\s+da\s+fatura\s*\n?\s*anterior\s*\([^)\n]*\)\s*\n?\s*R\$\s*" + Value, RegexOptions.IgnoreCase)]
    private static partial Regex PanPrevious();

    // "Fatura de agosto Limite total Vencimento" / "R$ 54,00 R$ 40.990,00 12/08/2026"
    [GeneratedRegex(@"^Fatura\s+de\s+\p{L}+.*Vencimento\s*\n[^\n]*?" + DateToken + @"\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex PanDue();

    [GeneratedRegex(@"fechamento\s+desta\s+fatura\s*\(\s*" + DateToken + @"\s*\)", RegexOptions.IgnoreCase)]
    private static partial Regex PanClosing();

    [GeneratedRegex(@"Pr[oó]xima\s+fatura[^\n]*\n(?:[^\n]*\n){0,2}?[^\n]*?Fechamento:\s*" + DateToken, RegexOptions.IgnoreCase)]
    private static partial Regex PanNextClosing();
}
