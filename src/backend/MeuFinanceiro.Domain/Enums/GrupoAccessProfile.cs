namespace MeuFinanceiro.Domain.Enums;

/// <summary>
/// Perfil de acesso de um GrupoMember dentro do próprio grupo (regra 22). Owner é
/// sempre Completo, nunca pode ser rebaixado (GrupoService.UpdateMemberProfileAsync).
/// </summary>
public enum GrupoAccessProfile
{
    Completo = 0,
    Editor = 1,
    Visualizador = 2
}
