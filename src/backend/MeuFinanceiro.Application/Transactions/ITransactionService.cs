using MeuFinanceiro.Application.Common;

namespace MeuFinanceiro.Application.Transactions;

public interface ITransactionService
{
    Task<PagedResult<TransactionResponse>> GetAllAsync(TransactionFilter filter, CancellationToken ct = default);
    Task<TransactionResponse> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<TransactionResponse>> CreateAsync(CreateTransactionRequest request, CancellationToken ct = default);
    Task<TransactionResponse> UpdateAsync(Guid id, UpdateTransactionRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
