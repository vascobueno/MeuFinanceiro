namespace MeuFinanceiro.Application.Installments;

public interface IInstallmentPlanService
{
    Task<InstallmentPlanResponse> CreateAsync(CreateInstallmentPlanRequest request, CancellationToken ct = default);
}
