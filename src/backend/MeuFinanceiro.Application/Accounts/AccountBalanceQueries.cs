using MeuFinanceiro.Domain.Entities;
using MeuFinanceiro.Domain.Enums;

namespace MeuFinanceiro.Application.Accounts;

/// <summary>Movimento líquido de uma conta até uma data (sem o saldo inicial).</summary>
public record AccountMovementsRow(Guid AccountId, decimal Total);

/// <summary>Conferência do cache de fechamento contra a soma bruta dos lançamentos.</summary>
public record AccountBalanceCheck(Guid AccountId, string AccountName, decimal ViaFechamento, decimal ViaSomaBruta)
{
    public decimal Diferenca => ViaFechamento - ViaSomaBruta;
    public bool Ok => Diferenca == 0m;
}

/// <summary>
/// Definição única de como o saldo de uma conta é apurado. Existe porque o mesmo cálculo estava
/// escrito de duas formas diferentes — e elas discordavam: a tela de Contas somava inclusive
/// lançamentos de data futura, enquanto a Projeção cortava em hoje.
///
/// A agregação inteira (quem entra, o sinal e o corte de data) fica neste único método em vez de
/// expressões soltas, porque o EF não traduz chamada a método C# dentro da consulta — separar as
/// partes obrigaria a reescrever o ternário em cada chamador, que é exatamente a duplicação que
/// causou a divergência.
/// </summary>
public static class AccountBalanceQueries
{
    public static IQueryable<AccountMovementsRow> ToAccountMovements(
        this IQueryable<FinancialTransaction> source,
        DateOnly asOf) =>
        source
            .Where(t => t.AccountId != null
                     && t.Status != TransactionStatus.Pending
                     && t.TransactionDate <= asOf)
            .GroupBy(t => t.AccountId!.Value)
            .Select(g => new AccountMovementsRow(
                g.Key,
                g.Sum(t => t.Direction == TransactionDirection.Credit ? t.Amount : -t.Amount)));
}
