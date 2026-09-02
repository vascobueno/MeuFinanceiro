using MeuFinanceiro.Domain.Enums;

namespace MeuFinanceiro.Application.Grupos;

public record GrupoMemberResponse(Guid UserId, string Name, string Email, GrupoRole Role, GrupoAccessProfile AccessProfile);

public record GrupoResponse(Guid Id, string Name, IReadOnlyList<GrupoMemberResponse> Members);

public record AddMemberRequest(string Email, GrupoRole Role);

public record CreateMemberRequest(string Name, string Username, string Email, GrupoRole Role);

public record UpdateMemberAccessProfileRequest(GrupoAccessProfile AccessProfile);
