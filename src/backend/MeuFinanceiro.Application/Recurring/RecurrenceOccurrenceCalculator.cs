using MeuFinanceiro.Domain.Entities;
using MeuFinanceiro.Domain.Enums;

namespace MeuFinanceiro.Application.Recurring;

/// <summary>
/// Lógica de datas de ocorrência de uma recorrência, compartilhada entre a Projeção
/// (docs/11-projecao-financeira.md) e a prévia de próximas ocorrências na tela Recorrências.
/// A ocorrência da própria StartDate já foi registrada como o primeiro lançamento ao criar a
/// recorrência, então toda enumeração começa a partir da ocorrência seguinte.
///
/// Cada ocorrência é calculada a partir da StartDate (StartDate + n períodos), nunca avançando
/// a partir da ocorrência anterior. Isso importa por dois motivos:
///
/// 1. CORREÇÃO. `AddMonths` encurta a data quando o mês de destino é mais curto, e avançar de
///    ocorrência em ocorrência faz esse encurtamento se acumular: uma conta que vence todo dia 31
///    virava 28/02 e daí em diante ficava presa no dia 28 para sempre (31/01 → 28/02 → 28/03 →
///    28/04). Calculando a partir da origem, o dia 31 volta assim que o mês permite
///    (31/01 → 28/02 → 31/03 → 30/04), que é como vencimento de fatura/aluguel funciona.
///
/// 2. CUSTO. Achar a primeira ocorrência futura deixa de depender da idade da recorrência. Antes
///    era um laço período a período desde a StartDate descartando o passado — uma recorrência
///    semanal de 3 anos atrás custava ~156 iterações inúteis antes da primeira útil.
/// </summary>
public static class RecurrenceOccurrenceCalculator
{
    public static DateOnly Advance(DateOnly date, RecurrenceFrequency frequency) => OccurrenceAt(date, frequency, 1);

    /// <summary>A n-ésima ocorrência contada a partir de <paramref name="startDate"/>.</summary>
    public static DateOnly OccurrenceAt(DateOnly startDate, RecurrenceFrequency frequency, int n) => frequency switch
    {
        RecurrenceFrequency.Weekly => startDate.AddDays(7 * n),
        RecurrenceFrequency.Yearly => startDate.AddYears(n),
        _ => startDate.AddMonths(n)
    };

    /// <summary>
    /// Índice da primeira ocorrência estritamente posterior a <paramref name="today"/>. Estima
    /// direto pela distância e ajusta com um laço curto — a estimativa pode errar por um por causa
    /// do encurtamento de meses curtos, nunca mais que isso.
    /// </summary>
    private static int FirstIndexAfter(DateOnly startDate, RecurrenceFrequency frequency, DateOnly today)
    {
        if (today < startDate) return 1;

        var estimate = frequency switch
        {
            RecurrenceFrequency.Weekly => (today.DayNumber - startDate.DayNumber) / 7,
            RecurrenceFrequency.Yearly => today.Year - startDate.Year,
            _ => (today.Year - startDate.Year) * 12 + today.Month - startDate.Month
        };

        var n = Math.Max(estimate, 0);
        while (OccurrenceAt(startDate, frequency, n) > today) n--;   // recua se passou do ponto
        while (OccurrenceAt(startDate, frequency, n) <= today) n++;  // avança até ultrapassar hoje
        return Math.Max(n, 1);
    }

    /// <summary>Ocorrências futuras (após "today") até uma data limite.</summary>
    public static IEnumerable<DateOnly> EnumerateUntil(RecurringTransaction recurrence, DateOnly today, DateOnly untilDate)
    {
        var n = FirstIndexAfter(recurrence.StartDate, recurrence.Frequency, today);

        while (true)
        {
            var occurrence = OccurrenceAt(recurrence.StartDate, recurrence.Frequency, n);
            if (occurrence > untilDate) yield break;
            if (recurrence.EndDate is not null && occurrence > recurrence.EndDate) yield break;

            yield return occurrence;
            n++;
        }
    }

    /// <summary>As próximas N ocorrências a partir de hoje, para prévia na listagem.</summary>
    public static IEnumerable<DateOnly> NextOccurrences(RecurringTransaction recurrence, DateOnly today, int count)
    {
        var n = FirstIndexAfter(recurrence.StartDate, recurrence.Frequency, today);

        for (var i = 0; i < count; i++, n++)
        {
            var occurrence = OccurrenceAt(recurrence.StartDate, recurrence.Frequency, n);
            if (recurrence.EndDate is not null && occurrence > recurrence.EndDate) yield break;
            yield return occurrence;
        }
    }
}
