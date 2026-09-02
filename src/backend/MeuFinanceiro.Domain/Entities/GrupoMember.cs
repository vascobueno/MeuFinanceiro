using MeuFinanceiro.Domain.Common;
using MeuFinanceiro.Domain.Enums;

namespace MeuFinanceiro.Domain.Entities;

/// <summary>Vínculo de um usuário a um grupo, com o papel dele nesse grupo (regra 22).</summary>
public class GrupoMember : AuditableEntity
{
    public Guid GrupoId { get; set; }
    public Guid UserId { get; set; }
    public GrupoRole Role { get; set; }
    public GrupoAccessProfile AccessProfile { get; set; } = GrupoAccessProfile.Completo;
}
