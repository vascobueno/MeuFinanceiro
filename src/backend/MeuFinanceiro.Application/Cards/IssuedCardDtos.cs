using MeuFinanceiro.Domain.Enums;

namespace MeuFinanceiro.Application.Cards;

/// <summary>
/// Não há campo de número completo — nem aqui nem em lugar nenhum. Quem colar o número inteiro na
/// tela tem o meio descartado lá mesmo; se chegar aqui mais do que 6 primeiros ou 4 últimos, é recusado.
/// </summary>
public record IssuedCardRequest(
    IssuedCardKind Kind,
    string LastFour,
    string? FirstDigits = null,
    int? ExpiryMonth = null,
    int? ExpiryYear = null,
    string? HolderName = null);

public record IssuedCardResponse(
    Guid Id,
    Guid CreditCardId,
    IssuedCardKind Kind,
    string? FirstDigits,
    string LastFour,
    int? ExpiryMonth,
    int? ExpiryYear,
    string? HolderName,
    IssuedCardStatus Status,
    /// <summary>Validade já passou — calculado, não é status.</summary>
    bool IsExpired,
    Guid? ReplacedById,
    /// <summary>A bandeira que os primeiros dígitos indicam, quando não há dúvida.</summary>
    string? SuggestedBrand,
    /// <summary>Os dígitos indicam outra bandeira que a do grupo: aviso, não bloqueio.</summary>
    bool BrandMismatch);
