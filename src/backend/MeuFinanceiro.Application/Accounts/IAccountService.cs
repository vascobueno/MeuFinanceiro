using MeuFinanceiro.Application.Common;

namespace MeuFinanceiro.Application.Accounts;

public interface IAccountService
{
    Task<PagedResult<AccountResponse>> GetAllAsync(PagedRequest request, CancellationToken ct = default);
    Task<AccountResponse> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<AccountResponse> CreateAsync(CreateAccountRequest request, CancellationToken ct = default);
    Task<AccountResponse> UpdateAsync(Guid id, UpdateAccountRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
