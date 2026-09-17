using MeuFinanceiro.Domain.Enums;

namespace MeuFinanceiro.Application.Projection;

/// <summary>
/// Uma movimentação (real ou projetada) que compõe a linha do tempo (docs/11-projecao-financeira.md).
/// Type permite ao relatório de "Projeção de receitas" (Parte 2 do plano) filtrar só Income —
/// Amount já vem assinado (crédito positivo/débito negativo), Type é a direção "crua".
/// </summary>
public record ProjectionEntry(DateOnly Date, string Description, decimal Amount, bool IsProjected, TransactionType Type);

/// <summary>
/// docs/11-projecao-financeira.md: mostra separadamente saldo projetado de contas, de
/// investimentos e o patrimônio consolidado (soma apenas para apresentação, regra 8).
/// Investimentos não têm rentabilidade futura inventada — usa-se a posição atual.
/// </summary>
public record ProjectionResponse(
    DateOnly Date,
    decimal CurrentAccountsBalance,
    decimal ProjectedAccountsBalance,
    decimal InvestmentsBalance,
    decimal ProjectedNetWorth,
    bool HasNegativeAccountsBalance,
    IReadOnlyList<ProjectionEntry> Timeline,
    // Agregados da linha do tempo COMPLETA, mesmo quando Timeline vem truncada por
    // maxTimelineEntries. Existem porque o Dashboard precisava só de um total e de uma contagem,
    // e para obtê-los baixava a linha inteira — medido em 1,5 MB numa única projeção de 60 dias.
    int TimelineCount,
    decimal ProjectedExpensesTotal
);
