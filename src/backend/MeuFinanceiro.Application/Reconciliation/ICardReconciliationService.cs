namespace MeuFinanceiro.Application.Reconciliation;

public interface ICardReconciliationService
{
    Task<CardInvoiceResponse> ImportAsync(ImportCardInvoiceRequest request, CancellationToken ct = default);
    Task<Import.ParsedCardInvoiceResponse> ParseAsync(Import.ParseCardInvoiceRequest request, CancellationToken ct = default);
    Task DeleteInvoiceAsync(Guid invoiceId, CancellationToken ct = default);
    Task<IReadOnlyList<CardReconciliationResponse>> GetAllAsync(Guid? creditCardId, string? status, CancellationToken ct = default);
    Task<CardReconciliationResponse> AcceptAsync(Guid reconciliationId, CancellationToken ct = default);
    Task<CardReconciliationResponse> LinkManuallyAsync(ManualCardReconciliationRequest request, CancellationToken ct = default);
    Task<CardReconciliationResponse> CreateTransactionFromItemAsync(Guid reconciliationId, CreateFromInvoiceItemRequest request, CancellationToken ct = default);
    Task UndoAsync(Guid reconciliationId, CancellationToken ct = default);
}
