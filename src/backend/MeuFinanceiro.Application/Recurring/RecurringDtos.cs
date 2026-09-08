using MeuFinanceiro.Domain.Enums;

namespace MeuFinanceiro.Application.Recurring;

/// <summary>Tela Recorrência (docs/screens/recorrencia): estado atual + versão vigente na data de hoje.</summary>
public record RecurringTransactionResponse(
    Guid Id,
    string Description,
    TransactionType Type,
    decimal CurrentAmount,
    RecurrenceFrequency Frequency,
    string? AccountName,
    string? CreditCardName,
    DateOnly StartDate,
    DateOnly? EndDate,
    bool IsActive,
    IReadOnlyList<RecurringOccurrencePreview> NextOccurrences
);

/// <summary>Prévia de uma ocorrência futura ainda não materializada (mesmo cálculo da Projeção).</summary>
public record RecurringOccurrencePreview(DateOnly Date, decimal Amount);

/// <summary>Regra 13: cria nova versão por data de vigência; ocorrências anteriores/conciliadas não mudam.</summary>
public record CreateRecurringVersionRequest(DateOnly EffectiveFrom, decimal Amount);
