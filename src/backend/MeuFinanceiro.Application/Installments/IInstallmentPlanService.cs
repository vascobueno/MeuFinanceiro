using MeuFinanceiro.Application.Common;

namespace MeuFinanceiro.Application.Installments;

public interface IInstallmentPlanService
{
    Task<InstallmentPlanResponse> CreateAsync(CreateInstallmentPlanRequest request, CancellationToken ct = default);

    Task<PagedResult<InstallmentPlanListItemResponse>> GetAllAsync(PagedRequest request, CancellationToken ct = default);

    Task<InstallmentPlanDetailResponse> GetByIdAsync(Guid id, CancellationToken ct = default);
}
