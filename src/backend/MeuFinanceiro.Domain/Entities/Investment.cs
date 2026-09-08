using MeuFinanceiro.Domain.Common;

namespace MeuFinanceiro.Domain.Entities;

public class Investment : AuditableEntity
{
    public Guid InvestmentAccountId { get; set; }
    public InvestmentAccount? InvestmentAccount { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Type { get; set; } // ex.: CDB DI
    public DateOnly? MaturityDate { get; set; }
    public string? Liquidity { get; set; }

    public ICollection<InvestmentPosition> Positions { get; set; } = new List<InvestmentPosition>();
    public ICollection<InvestmentTransaction> Transactions { get; set; } = new List<InvestmentTransaction>();
}
