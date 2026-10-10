using MeuFinanceiro.Domain.Enums;

namespace MeuFinanceiro.Application.Cards;

/// <summary>
/// Não há campo de número completo — nem aqui nem em lugar nenhum. Quem colar o número inteiro na
/// tela tem o meio descartado lá mesmo; se chegar aqui mais do que 6 primeiros ou 4 últimos, é recusado.
/// </summary>
/// <param name="AccountId">Conta do débito. No cartão do grupo de crédito, preenchido o torna
/// múltiplo; nulo, só de crédito. No cartão só de débito, é obrigatório na edição.</param>
/// <param name="Brand">Só para o cartão só de débito — no de crédito vale a bandeira do grupo.</param>
public record IssuedCardRequest(
    IssuedCardKind Kind,
    string LastFour,
    string? FirstDigits = null,
    int? ExpiryMonth = null,
    int? ExpiryYear = null,
    string? HolderName = null,
    Guid? AccountId = null,
    string? Brand = null);

/// <summary>Calculada pelas ligações: grupo de crédito, conta de débito ou as duas.</summary>
public enum IssuedCardFunction
{
    Credito = 1,
    Debito = 2,
    Multiplo = 3
}

public record IssuedCardResponse(
    Guid Id,
    Guid? CreditCardId,
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
    bool BrandMismatch,
    IssuedCardFunction Function,
    Guid? AccountId,
    string? AccountName,
    string? CreditCardName,
    /// <summary>A bandeira que vale: a do grupo no crédito, a própria no só de débito.</summary>
    string? Brand);
