namespace MeuFinanceiro.Application.Merchants;

public record MerchantSuggestionResponse(Guid Id, string Name, Guid? DefaultCategoryId);
