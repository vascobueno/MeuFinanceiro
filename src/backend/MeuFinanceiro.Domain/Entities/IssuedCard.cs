using MeuFinanceiro.Domain.Common;
using MeuFinanceiro.Domain.Enums;

namespace MeuFinanceiro.Domain.Entities;

/// <summary>
/// Um cartão de plástico ou virtual de um grupo de cartões (<see cref="CreditCard"/>, que é a
/// fatura). Titular, adicional e virtual caem na mesma fatura; um cartão substituído ou vencido
/// ainda aparece nela enquanto houver parcelas ou compras do período em que valia.
///
/// O número completo nunca é guardado (nem o CVV): só os primeiros dígitos, que dizem a bandeira,
/// e os 4 últimos, que são o que a fatura imprime em cada compra.
/// </summary>
public class IssuedCard : GrupoOwnedEntity
{
    public Guid CreditCardId { get; set; }
    public CreditCard? CreditCard { get; set; }

    public IssuedCardKind Kind { get; set; } = IssuedCardKind.Titular;

    /// <summary>De 4 a 6 primeiros dígitos (o BIN). Opcional: a fatura mostra no máximo 4, às vezes nenhum.</summary>
    public string? FirstDigits { get; set; }

    /// <summary>Os 4 últimos dígitos. Únicos no grupo entre os cartões ativos.</summary>
    public string LastFour { get; set; } = string.Empty;

    public int? ExpiryMonth { get; set; }
    public int? ExpiryYear { get; set; }
    public string? HolderName { get; set; }

    public IssuedCardStatus Status { get; set; } = IssuedCardStatus.Ativo;

    /// <summary>O cartão que substituiu este, quando <see cref="Status"/> é Substituido.</summary>
    public Guid? ReplacedById { get; set; }
    public IssuedCard? ReplacedBy { get; set; }
}
