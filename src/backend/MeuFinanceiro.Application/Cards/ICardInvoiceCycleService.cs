namespace MeuFinanceiro.Application.Cards;

/// <summary>Fatura de um ciclo de cartão, calculada a partir dos lançamentos (docs/07).</summary>
public interface ICardInvoiceCycleService
{
    /// <summary>
    /// <paramref name="reference"/> é a data de fechamento do ciclo; omitida, devolve o ciclo atual.
    /// </summary>
    Task<CardInvoiceCycleResponse> GetAsync(Guid cardId, DateOnly? reference, CancellationToken ct = default);

    /// <summary>Ajusta o fechamento de um mês — quando a operadora antecipa ou adia.</summary>
    Task<CardInvoiceCycleResponse> SetClosingAsync(Guid cardId, DateOnly reference, DateOnly closingDate, CancellationToken ct = default);

    /// <summary>Remove o ajuste e volta ao dia de fechamento do cadastro.</summary>
    Task<CardInvoiceCycleResponse> ClearClosingAsync(Guid cardId, DateOnly reference, CancellationToken ct = default);
}
