namespace MeuFinanceiro.Application.Cards;

// Brand vai e volta como o nome da bandeira (CardBrand: "Visa", "Mastercard", "AmericanExpress"…).
// Na entrada também valem os apelidos de antes ("Master", "Amex") — ver CardBrands.

/// <param name="Titular">Cartão do titular, criado junto com o grupo, na mesma gravação: se ele for
/// recusado (final repetido, número completo), o grupo também não é criado.</param>
public record CreateCardRequest(string Name, string? Brand, int ClosingDay, int DueDay, decimal? Limit,
    IssuedCardRequest? Titular = null);
public record UpdateCardRequest(string Name, string? Brand, int ClosingDay, int DueDay, decimal? Limit, bool IsActive);

public record CardResponse(Guid Id, string Name, string? Brand, int ClosingDay, int DueDay, decimal? Limit, bool IsActive);
