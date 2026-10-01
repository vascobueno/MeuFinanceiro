namespace MeuFinanceiro.Application.Cards;

/// <summary>Um ciclo de fatura: o período que ele cobre, quando fecha e quando vence.</summary>
public record CardCycle(DateOnly Start, DateOnly ClosingDate, DateOnly DueDate);

/// <summary>
/// Traduz o dia de fechamento e o dia de vencimento de um cartão em ciclos de fatura concretos.
///
/// Até aqui `ClosingDay`/`DueDay` eram só campos de cadastro — não entravam em cálculo nenhum, e
/// não havia como dizer a que fatura um lançamento pertence. Esta classe é essa regra.
///
/// Cada ciclo é calculado a partir do seu próprio mês de referência, nunca avançando a partir do
/// ciclo anterior. Isso não é preferência: `AddMonths` encurta a data quando o mês de destino é
/// mais curto, e avançar de ciclo em ciclo acumula esse encurtamento — um fechamento dia 31 viraria
/// 28 em fevereiro e ficaria preso no 28 para sempre. Calculando pelo mês, o dia 31 volta assim que
/// o mês permite (31/01 → 28/02 → 31/03 → 30/04). É o mesmo erro que já corrigimos em
/// RecurrenceOccurrenceCalculator, e por isso `Shift` recalcula pelo mês em vez de somar período.
///
/// Pura de propósito: é calendário, sem EF e sem I/O, para poder ser coberta por teste unitário.
/// </summary>
public static class CardCycleCalculator
{
    /// <summary>O ciclo cujo fechamento cai no mês da data informada.</summary>
    public static CardCycle CycleForMonth(int closingDay, int dueDay, DateOnly anyDateInMonth)
    {
        var closing = ClosingIn(anyDateInMonth.Year, anyDateInMonth.Month, closingDay);

        var previous = FirstOfMonth(anyDateInMonth).AddMonths(-1);
        var start = ClosingIn(previous.Year, previous.Month, closingDay).AddDays(1);

        return new CardCycle(start, closing, DueAfter(closing, dueDay));
    }

    /// <summary>
    /// O ciclo a que uma data pertence: o primeiro cujo fechamento seja igual ou posterior a ela.
    /// Compra no próprio dia do fechamento entra nesse ciclo; um dia depois cai no seguinte.
    /// </summary>
    public static CardCycle CycleOf(int closingDay, int dueDay, DateOnly date)
    {
        var cycle = CycleForMonth(closingDay, dueDay, date);
        return date <= cycle.ClosingDate
            ? cycle
            : CycleForMonth(closingDay, dueDay, FirstOfMonth(date).AddMonths(1));
    }

    /// <summary>Anda <paramref name="months"/> ciclos (negativo volta) — navegação da tela.</summary>
    public static CardCycle Shift(int closingDay, int dueDay, CardCycle from, int months) =>
        CycleForMonth(closingDay, dueDay, FirstOfMonth(from.ClosingDate).AddMonths(months));

    /// <summary>
    /// O vencimento de um fechamento qualquer — inclusive de um ajustado por fatura
    /// (CardCycleClosing). Exposto para que a regra viva num lugar só: duplicá-la foi exatamente
    /// o que produziu o bug de navegação no cliente.
    /// </summary>
    public static DateOnly DueDateFor(DateOnly closing, int dueDay) => DueAfter(closing, dueDay);

    private static DateOnly FirstOfMonth(DateOnly date) => new(date.Year, date.Month, 1);

    /// <summary>O fechamento num mês, encurtado quando o mês não tem aquele dia.</summary>
    private static DateOnly ClosingIn(int year, int month, int closingDay) =>
        new(year, month, Math.Min(Math.Max(closingDay, 1), DateTime.DaysInMonth(year, month)));

    /// <summary>
    /// O vencimento é a próxima ocorrência do dia de vencimento DEPOIS do fechamento: mesmo mês
    /// quando o dia de vencimento é maior que o de fechamento, mês seguinte caso contrário.
    /// </summary>
    private static DateOnly DueAfter(DateOnly closing, int dueDay)
    {
        var sameMonth = DayIn(closing.Year, closing.Month, dueDay);
        if (sameMonth > closing) return sameMonth;

        var next = FirstOfMonth(closing).AddMonths(1);
        return DayIn(next.Year, next.Month, dueDay);
    }

    private static DateOnly DayIn(int year, int month, int day) =>
        new(year, month, Math.Min(Math.Max(day, 1), DateTime.DaysInMonth(year, month)));
}
