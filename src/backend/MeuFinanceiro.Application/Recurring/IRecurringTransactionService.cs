using MeuFinanceiro.Domain.Enums;

namespace MeuFinanceiro.Application.Recurring;

public interface IRecurringTransactionService
{
    /// <summary>type opcional (ex.: Expense) — usado pelo relatório "Gastos recorrentes" (Parte 2 do plano).</summary>
    Task<IReadOnlyList<RecurringTransactionResponse>> GetAllAsync(TransactionType? type = null, CancellationToken ct = default);

    Task<RecurringTransactionResponse> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<RecurringTransactionResponse> CreateVersionAsync(Guid id, CreateRecurringVersionRequest request, CancellationToken ct = default);
}
