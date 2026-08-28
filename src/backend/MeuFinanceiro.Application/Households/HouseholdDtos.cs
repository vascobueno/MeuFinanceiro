using MeuFinanceiro.Domain.Enums;

namespace MeuFinanceiro.Application.Households;

public record HouseholdMemberResponse(Guid UserId, string Name, string Email, HouseholdRole Role, HouseholdAccessProfile AccessProfile);

public record HouseholdResponse(Guid Id, string Name, IReadOnlyList<HouseholdMemberResponse> Members);

public record AddMemberRequest(string Email, HouseholdRole Role);

public record UpdateMemberAccessProfileRequest(HouseholdAccessProfile AccessProfile);
