using MeuFinanceiro.Domain.Common;
using MeuFinanceiro.Domain.Enums;

namespace MeuFinanceiro.Domain.Entities;

/// <summary>Vínculo de um usuário a um household, com o papel dele nesse household (regra 22).</summary>
public class HouseholdMember : AuditableEntity
{
    public Guid HouseholdId { get; set; }
    public Guid UserId { get; set; }
    public HouseholdRole Role { get; set; }
    public HouseholdAccessProfile AccessProfile { get; set; } = HouseholdAccessProfile.Completo;
}
