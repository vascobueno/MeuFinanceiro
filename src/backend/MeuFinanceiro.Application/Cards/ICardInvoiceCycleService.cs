namespace MeuFinanceiro.Application.Cards;

/// <summary>Fatura de um ciclo de cartão, calculada a partir dos lançamentos (docs/07).</summary>
public interface ICardInvoiceCycleService
{
    /// <summary>
    /// <paramref name="reference"/> é a data de fechamento do ciclo; omitida, devolve o ciclo atual.
    /// </summary>
    Task<CardInvoiceCycleResponse> GetAsync(Guid cardId, DateOnly? reference, CancellationToken ct = default);
}
