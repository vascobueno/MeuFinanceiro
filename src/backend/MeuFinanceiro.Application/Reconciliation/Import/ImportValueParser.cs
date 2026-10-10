using System.Globalization;
using System.Text.RegularExpressions;

namespace MeuFinanceiro.Application.Reconciliation.Import;

/// <summary>
/// Valores e datas como aparecem nos extratos e faturas dos bancos brasileiros.
///
/// Antes a tela fazia <c>Number(valor)</c>: qualquer valor no formato do extrato
/// (<c>1.288,46-</c>, <c>25,00</c>) virava <c>NaN</c>, ia como <c>null</c> e a importação era
/// recusada com um erro técnico em inglês. Datas como <c>02/01</c> também eram recusadas.
/// </summary>
public static partial class ImportValueParser
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    /// <summary>
    /// Aceita <c>1.288,46-</c> (sinal no fim, como no extrato), <c>-1.288,46</c>,
    /// <c>-R$ 54,00</c>, <c>R$ 1.234,56</c>, <c>25,00</c> e o formato antigo <c>-1288.46</c>.
    /// </summary>
    public static bool TryParseMoney(string? raw, out decimal value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(raw)) return false;

        var s = raw.Trim().Replace(" ", string.Empty);
        var negative = false;

        if (s.EndsWith('-')) { negative = true; s = s[..^1]; }
        if (s.StartsWith('-')) { negative = true; s = s[1..]; }
        else if (s.StartsWith('+')) s = s[1..];
        if (s.StartsWith("R$", StringComparison.OrdinalIgnoreCase)) s = s[2..];
        // "R$ -54,00": o sinal pode vir depois do símbolo.
        if (s.StartsWith('-')) { negative = true; s = s[1..]; }

        if (s.Length == 0) return false;

        string normalized;
        if (s.Contains(','))
        {
            // Formato brasileiro: ponto de milhar opcional, vírgula decimal.
            if (!BrazilianNumber().IsMatch(s)) return false;
            normalized = s.Replace(".", string.Empty).Replace(',', '.');
        }
        else
        {
            if (!PlainNumber().IsMatch(s)) return false;
            normalized = s;
        }

        if (!decimal.TryParse(normalized, NumberStyles.AllowDecimalPoint, Invariant, out value)) return false;
        if (negative) value = -value;
        return true;
    }

    /// <summary>
    /// Token que só pode ser dinheiro: exige exatamente duas casas decimais. É o que separa o
    /// valor do número de documento (<c>200652</c>), da parcela (<c>04/06</c>) e da cotação
    /// (<c>5,4970</c>, quatro casas).
    /// </summary>
    public static bool IsMoneyToken(string token)
    {
        var s = token.Trim();
        if (s.EndsWith('-')) s = s[..^1];
        if (s.StartsWith('-') || s.StartsWith('+')) s = s[1..];
        if (s.StartsWith("R$", StringComparison.OrdinalIgnoreCase)) s = s[2..];
        if (s.StartsWith('-')) s = s[1..];
        return MoneyShape().IsMatch(s);
    }

    /// <summary>
    /// Data de uma linha. <c>dd/MM</c> não traz ano, então ele vem da data de referência
    /// (fim do extrato, fechamento da fatura): se a data cairia DEPOIS da referência, é do ano
    /// anterior — é o caso de <c>31/12</c> num extrato de janeiro, ou da compra de dezembro numa
    /// fatura de janeiro, ou da parcela cuja compra foi meses antes.
    /// </summary>
    public static bool TryParseDate(string? raw, DateOnly reference, out DateOnly date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(raw)) return false;
        var s = raw.Trim();

        if (DateOnly.TryParseExact(s, "yyyy-MM-dd", Invariant, DateTimeStyles.None, out date)) return true;
        if (DateOnly.TryParseExact(s, "dd/MM/yyyy", Invariant, DateTimeStyles.None, out date)) return true;
        if (DateOnly.TryParseExact(s, "dd/MM/yy", Invariant, DateTimeStyles.None, out date)) return true;

        var m = DayMonth().Match(s);
        if (!m.Success) return false;

        var day = int.Parse(m.Groups[1].Value, Invariant);
        var month = int.Parse(m.Groups[2].Value, Invariant);
        if (month is < 1 or > 12) return false;

        // Uma semana de folga: extratos e faturas às vezes trazem lançamentos datados logo
        // depois do fim do período, e isso não pode virar "ano anterior".
        var limit = reference.AddDays(7);
        foreach (var year in new[] { reference.Year, reference.Year - 1 })
        {
            if (day > DateTime.DaysInMonth(year, month)) continue;
            var candidate = new DateOnly(year, month, day);
            if (candidate <= limit)
            {
                date = candidate;
                return true;
            }
        }
        return false;
    }

    public static bool LooksLikeDate(string token) =>
        DayMonth().IsMatch(token) || FullDate().IsMatch(token) || IsoDate().IsMatch(token);

    public static string FormatBrl(decimal value) =>
        value.ToString("C", CultureInfo.GetCultureInfo("pt-BR"));

    [GeneratedRegex(@"^(?:\d{1,3}(?:\.\d{3})+|\d+),\d{2}$")]
    private static partial Regex BrazilianNumber();

    [GeneratedRegex(@"^\d+(?:\.\d{1,2})?$")]
    private static partial Regex PlainNumber();

    [GeneratedRegex(@"^(?:(?:\d{1,3}(?:\.\d{3})+|\d+),\d{2}|\d+\.\d{2})$")]
    private static partial Regex MoneyShape();

    [GeneratedRegex(@"^(\d{2})/(\d{2})$")]
    private static partial Regex DayMonth();

    [GeneratedRegex(@"^\d{2}/\d{2}/\d{2,4}$")]
    private static partial Regex FullDate();

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}$")]
    private static partial Regex IsoDate();
}
