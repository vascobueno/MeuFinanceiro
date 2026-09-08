using MeuFinanceiro.Domain.Enums;

namespace MeuFinanceiro.Application.Admin;

public record AdminGrupoResponse(Guid Id, string Name, string OwnerName, string OwnerEmail, int MemberCount, DateTime CreatedAt);

/// <summary>Visão achatada de um membro, com o grupo a que pertence — usada pela tela
/// "Perfis de acesso" (cross-grupo), que evita o Administrador ter que entrar grupo
/// por grupo só para ajustar o AccessProfile de alguém.</summary>
public record AdminMemberResponse(
    Guid GrupoId,
    string GrupoName,
    Guid UserId,
    string Name,
    string Email,
    GrupoRole Role,
    GrupoAccessProfile AccessProfile);
