using MeuFinanceiro.Application.Common;

namespace MeuFinanceiro.Application.Statements;

public interface IStatementService
{
    Task<PagedResult<BankStatementListItemResponse>> GetAllAsync(BankStatementFilter filter, CancellationToken ct = default);
}
