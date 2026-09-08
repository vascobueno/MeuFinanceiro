namespace MeuFinanceiro.Domain.Enums;

/// <summary>Lançamentos conciliados não são alterados automaticamente (regra 12).</summary>
public enum TransactionStatus
{
    Pending = 1,
    Confirmed = 2,
    Reconciled = 3
}
