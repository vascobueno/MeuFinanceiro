using MeuFinanceiro.Application.Investments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MeuFinanceiro.Api.Controllers;

/// <summary>docs/10-investimentos.md, docs/13-api.md.</summary>
[ApiController]
[Authorize]
[Route("api/v1/investments")]
public class InvestmentsController : ControllerBase
{
    private readonly IInvestmentService _service;

    public InvestmentsController(IInvestmentService service)
    {
        _service = service;
    }

    [HttpGet("summary")]
    public async Task<ActionResult<InvestmentsSummaryResponse>> GetSummary(CancellationToken ct)
        => Ok(await _service.GetSummaryAsync(ct));

    [HttpPost("accounts")]
    public async Task<ActionResult<InvestmentAccountResponse>> CreateAccount(CreateInvestmentAccountRequest request, CancellationToken ct)
        => Ok(await _service.CreateAccountAsync(request, ct));

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<InvestmentResponse>>> GetAll([FromQuery] Guid? investmentAccountId, CancellationToken ct)
        => Ok(await _service.GetInvestmentsAsync(investmentAccountId, ct));

    [HttpPost]
    public async Task<ActionResult<InvestmentResponse>> Create(CreateInvestmentRequest request, CancellationToken ct)
        => Ok(await _service.CreateInvestmentAsync(request, ct));

    [HttpPost("transactions")]
    public async Task<ActionResult<InvestmentTransactionResponse>> RegisterTransaction(CreateInvestmentTransactionRequest request, CancellationToken ct)
        => Ok(await _service.RegisterTransactionAsync(request, ct));
}
