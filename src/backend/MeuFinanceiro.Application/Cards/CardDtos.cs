namespace MeuFinanceiro.Application.Cards;

public record CreateCardRequest(string Name, string? Brand, int ClosingDay, int DueDay, decimal? Limit);
public record UpdateCardRequest(string Name, string? Brand, int ClosingDay, int DueDay, decimal? Limit, bool IsActive);

public record CardResponse(Guid Id, string Name, string? Brand, int ClosingDay, int DueDay, decimal? Limit, bool IsActive);
