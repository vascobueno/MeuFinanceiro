using MeuFinanceiro.Domain.Entities;
using MeuFinanceiro.Domain.Enums;

namespace MeuFinanceiro.Application.Recurring;

/// <summary>
/// Lógica de datas de ocorrência de uma recorrência, compartilhada entre a Projeção
/// (docs/11-projecao-financeira.md) e a prévia de próximas ocorrências na tela Recorrências.
/// A ocorrência da própria StartDate já foi registrada como o primeiro lançamento ao criar a
/// recorrência, então toda enumeração começa a partir da ocorrência seguinte.
/// </summary>
public static class RecurrenceOccurrenceCalculator
{
    public static DateOnly Advance(DateOnly date, RecurrenceFrequency frequency) => frequency switch
    {
        RecurrenceFrequency.Weekly => date.AddDays(7),
        RecurrenceFrequency.Monthly => date.AddMonths(1),
        RecurrenceFrequency.Yearly => date.AddYears(1),
        _ => date.AddMonths(1)
    };

    /// <summary>Ocorrências futuras (após "today") até uma data limite.</summary>
    public static IEnumerable<DateOnly> EnumerateUntil(RecurringTransaction recurrence, DateOnly today, DateOnly untilDate)
    {
        var cursor = Advance(recurrence.StartDate, recurrence.Frequency);

        while (cursor <= untilDate)
        {
            if (recurrence.EndDate is not null && cursor > recurrence.EndDate) yield break;
            if (cursor > today) yield return cursor;
            cursor = Advance(cursor, recurrence.Frequency);
        }
    }

    /// <summary>As próximas N ocorrências a partir de hoje, para prévia na listagem.</summary>
    public static IEnumerable<DateOnly> NextOccurrences(RecurringTransaction recurrence, DateOnly today, int count)
    {
        var cursor = Advance(recurrence.StartDate, recurrence.Frequency);
        while (cursor <= today) cursor = Advance(cursor, recurrence.Frequency);

        for (var i = 0; i < count; i++)
        {
            if (recurrence.EndDate is not null && cursor > recurrence.EndDate) yield break;
            yield return cursor;
            cursor = Advance(cursor, recurrence.Frequency);
        }
    }
}
