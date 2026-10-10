namespace MeuFinanceiro.Application.Cards;

// Brand vai e volta como o nome da bandeira (CardBrand: "Visa", "Mastercard", "AmericanExpress"…).
// Na entrada também valem os apelidos de antes ("Master", "Amex") — ver CardBrands.

public record CreateCardRequest(string Name, string? Brand, int ClosingDay, int DueDay, decimal? Limit);
public record UpdateCardRequest(string Name, string? Brand, int ClosingDay, int DueDay, decimal? Limit, bool IsActive);

public record CardResponse(Guid Id, string Name, string? Brand, int ClosingDay, int DueDay, decimal? Limit, bool IsActive);
