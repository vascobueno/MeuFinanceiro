using MeuFinanceiro.Application.Reconciliation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MeuFinanceiro.Api.Controllers;

/// <summary>docs/09-conciliacao-conta-corrente.md, docs/13-api.md.</summary>
[ApiController]
[Authorize]
[Route("api/v1")]
public class BankReconciliationController : ControllerBase
{
    private readonly IBankReconciliationService _service;

    public BankReconciliationController(IBankReconciliationService service)
    {
        _service = service;
    }

    [HttpPost("bank-statements/import")]
    public async Task<ActionResult<BankStatementResponse>> Import(ImportBankStatementRequest request, CancellationToken ct)
        => Ok(await _service.ImportAsync(request, ct));

    [HttpGet("reconciliations/bank")]
    public async Task<ActionResult<IReadOnlyList<BankReconciliationResponse>>> GetAll(
        [FromQuery] Guid? accountId, [FromQuery] string? status, CancellationToken ct)
        => Ok(await _service.GetAllAsync(accountId, status, ct));

    [HttpPost("reconciliations/bank/{id:guid}/accept")]
    public async Task<ActionResult<BankReconciliationResponse>> Accept(Guid id, CancellationToken ct)
        => Ok(await _service.AcceptAsync(id, ct));

    [HttpPost("reconciliations/bank/manual")]
    public async Task<ActionResult<BankReconciliationResponse>> LinkManually(ManualReconciliationRequest request, CancellationToken ct)
        => Ok(await _service.LinkManuallyAsync(request, ct));

    [HttpPost("reconciliations/bank/{id:guid}/undo")]
    public async Task<IActionResult> Undo(Guid id, CancellationToken ct)
    {
        await _service.UndoAsync(id, ct);
        return NoContent();
    }
}
