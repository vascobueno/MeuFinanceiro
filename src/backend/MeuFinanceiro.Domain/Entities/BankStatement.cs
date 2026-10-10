using MeuFinanceiro.Domain.Common;

namespace MeuFinanceiro.Domain.Entities;

/// <summary>docs/09-conciliacao-conta-corrente.md — extrato bancário importado (fonte externa).</summary>
public class BankStatement : GrupoOwnedEntity
{
    public Guid AccountId { get; set; }
    public Account? Account { get; set; }
    public DateOnly ReferenceStart { get; set; }
    public DateOnly ReferenceEnd { get; set; }
    public decimal OpeningBalance { get; set; }
    public decimal ClosingBalance { get; set; }
    public string? OriginalFileName { get; set; }
    public string? FileHash { get; set; }

    /// <summary>
    /// Impressão digital do CONTEÚDO (conta, período, saldos e itens normalizados), e não do
    /// arquivo: na colagem não há arquivo, e o mesmo arquivo renomeado passaria. Único por grupo —
    /// é o que impede o mesmo extrato de entrar duas vezes.
    /// </summary>
    public string? ContentHash { get; set; }

    /// <summary>Saldo inicial + soma dos movimentos bateu com o saldo final na importação.</summary>
    public bool BalanceValidated { get; set; }

    public ICollection<BankStatementItem> Items { get; set; } = new List<BankStatementItem>();
}
