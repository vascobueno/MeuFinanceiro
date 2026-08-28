using MeuFinanceiro.Domain.Common;

namespace MeuFinanceiro.Domain.Entities;

/// <summary>Dono real dos dados financeiros — vários usuários podem pertencer ao mesmo household.</summary>
public class Household : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
}
