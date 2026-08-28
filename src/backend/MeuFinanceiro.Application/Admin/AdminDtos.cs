using MeuFinanceiro.Domain.Enums;

namespace MeuFinanceiro.Application.Admin;

public record AdminHouseholdResponse(Guid Id, string Name, string OwnerName, string OwnerEmail, int MemberCount, DateTime CreatedAt);

/// <summary>Visão achatada de um membro, com o household a que pertence — usada pela tela
/// "Perfis de acesso" (cross-household), que evita o Administrador ter que entrar household
/// por household só para ajustar o AccessProfile de alguém.</summary>
public record AdminMemberResponse(
    Guid HouseholdId,
    string HouseholdName,
    Guid UserId,
    string Name,
    string Email,
    HouseholdRole Role,
    HouseholdAccessProfile AccessProfile);
