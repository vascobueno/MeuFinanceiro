namespace MeuFinanceiro.Application.Reconciliation;

public interface IBankReconciliationService
{
    Task<BankStatementResponse> ImportAsync(ImportBankStatementRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<BankReconciliationResponse>> GetAllAsync(Guid? accountId, string? status, CancellationToken ct = default);
    Task<BankReconciliationResponse> AcceptAsync(Guid reconciliationId, CancellationToken ct = default);
    Task<BankReconciliationResponse> LinkManuallyAsync(ManualReconciliationRequest request, CancellationToken ct = default);
    Task UndoAsync(Guid reconciliationId, CancellationToken ct = default);
}
