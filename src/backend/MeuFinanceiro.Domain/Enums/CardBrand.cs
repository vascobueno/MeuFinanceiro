namespace MeuFinanceiro.Domain.Enums;

/// <summary>
/// Bandeira (docs/04). Gravada pelo nome, não pelo número, para que acrescentar uma bandeira no
/// meio da lista não mude o significado das já gravadas. As três últimas são só de débito: valem
/// para o cartão de débito (etapa 7), não para o grupo de cartões de crédito.
/// </summary>
public enum CardBrand
{
    Visa,
    Mastercard,
    Elo,
    AmericanExpress,
    Hipercard,
    DinersClub,
    Jcb,
    Discover,
    UnionPay,
    Cabal,
    Outra,
    Maestro,
    VisaElectron,
    EloDebito
}
