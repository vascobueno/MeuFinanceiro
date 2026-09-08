using MeuFinanceiro.Application.Reconciliation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MeuFinanceiro.Api.Controllers;

/// <summary>docs/08-conciliacao-cartoes.md, docs/13-api.md.</summary>
[ApiController]
[Authorize]
[Route("api/v1")]
public class CardReconciliationController : ControllerBase
{
    private readonly ICardReconciliationService _service;

    public CardReconciliationController(ICardReconciliationService service)
    {
        _service = service;
    }

    [HttpPost("card-invoices/import")]
    public async Task<ActionResult<CardInvoiceResponse>> Import(ImportCardInvoiceRequest request, CancellationToken ct)
        => Ok(await _service.ImportAsync(request, ct));

    [HttpGet("reconciliations/card")]
    public async Task<ActionResult<IReadOnlyList<CardReconciliationResponse>>> GetAll(
        [FromQuery] Guid? creditCardId, [FromQuery] string? status, CancellationToken ct)
        => Ok(await _service.GetAllAsync(creditCardId, status, ct));

    [HttpPost("reconciliations/card/{id:guid}/accept")]
    public async Task<ActionResult<CardReconciliationResponse>> Accept(Guid id, CancellationToken ct)
        => Ok(await _service.AcceptAsync(id, ct));

    [HttpPost("reconciliations/card/manual")]
    public async Task<ActionResult<CardReconciliationResponse>> LinkManually(ManualCardReconciliationRequest request, CancellationToken ct)
        => Ok(await _service.LinkManuallyAsync(request, ct));

    [HttpPost("reconciliations/card/{id:guid}/undo")]
    public async Task<IActionResult> Undo(Guid id, CancellationToken ct)
    {
        await _service.UndoAsync(id, ct);
        return NoContent();
    }
}
