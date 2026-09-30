namespace MeuFinanceiro.Application.Cards;

/// <summary>Pagamentos de fatura de cartão (docs/05 — Pagamento de cartão).</summary>
public interface ICardPaymentService
{
    Task<CardPaymentResponse> CreateAsync(CreateCardPaymentRequest request, CancellationToken ct = default);
    Task<CardPaymentResponse> UpdateAsync(Guid id, UpdateCardPaymentRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
