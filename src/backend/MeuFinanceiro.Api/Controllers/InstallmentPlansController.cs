using MeuFinanceiro.Application.Installments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MeuFinanceiro.Api.Controllers;

/// <summary>Tela Parcelamento (docs/screens/parcelamento).</summary>
[ApiController]
[Authorize]
[Route("api/v1/installment-plans")]
public class InstallmentPlansController : ControllerBase
{
    private readonly IInstallmentPlanService _installmentPlanService;

    public InstallmentPlansController(IInstallmentPlanService installmentPlanService)
    {
        _installmentPlanService = installmentPlanService;
    }

    [HttpPost]
    public async Task<ActionResult<InstallmentPlanResponse>> Create(CreateInstallmentPlanRequest request, CancellationToken ct)
        => Ok(await _installmentPlanService.CreateAsync(request, ct));
}
