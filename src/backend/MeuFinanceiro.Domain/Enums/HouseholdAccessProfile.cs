namespace MeuFinanceiro.Domain.Enums;

/// <summary>
/// Perfil de acesso de um HouseholdMember dentro do próprio household (regra 22). Owner é
/// sempre Completo, nunca pode ser rebaixado (HouseholdService.UpdateMemberProfileAsync).
/// </summary>
public enum HouseholdAccessProfile
{
    Completo = 0,
    Editor = 1,
    Visualizador = 2
}
