namespace MeuFinanceiro.Domain.Enums;

/// <summary>
/// De onde o lançamento saiu, para as telas separadas de Lançamentos (docs/06). Os filtros por
/// AccountId/CreditCardId respondem "desta conta" / "deste cartão"; este responde "de qualquer
/// conta" / "de qualquer cartão", que não era expressável.
/// </summary>
public enum TransactionOrigin
{
    Account = 1,
    Card = 2
}
