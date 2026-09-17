using MeuFinanceiro.Application.Common;
using MeuFinanceiro.Domain.Enums;

namespace MeuFinanceiro.Application.Recurring;

public interface IRecurringTransactionService
{
    /// <summary>Listagem paginada da tela Recorrências.</summary>
    Task<PagedResult<RecurringTransactionResponse>> GetAllAsync(RecurringTransactionFilter filter, CancellationToken ct = default);

    /// <summary>
    /// Relatório imprimível: todos os itens ativos do tipo pedido, em shape enxuto. Não é
    /// paginado porque o total do rodapé precisa fechar sobre o conjunto inteiro.
    /// </summary>
    Task<IReadOnlyList<RecurringExpenseReportItem>> GetReportAsync(TransactionType? type = null, CancellationToken ct = default);

    Task<RecurringTransactionResponse> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<RecurringTransactionResponse> CreateVersionAsync(Guid id, CreateRecurringVersionRequest request, CancellationToken ct = default);

    /// <summary>Altera o cadastro (destino/categoria/vigência/ativa). O valor só muda por versão.</summary>
    Task<RecurringTransactionResponse> UpdateAsync(Guid id, UpdateRecurringTransactionRequest request, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
