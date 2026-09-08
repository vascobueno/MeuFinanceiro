namespace MeuFinanceiro.Domain.Enums;

/// <summary>docs/08-conciliacao-cartoes.md — faixas de score de conciliação.</summary>
public enum ReconciliationStatus
{
    Unmatched = 1,
    PossibleMatch = 2,
    Matched = 3,
    ManuallyMatched = 4,
    Ignored = 5,
    Duplicate = 6
}
