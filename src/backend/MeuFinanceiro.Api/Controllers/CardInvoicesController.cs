using MeuFinanceiro.Application.Common;
using MeuFinanceiro.Application.Invoices;
using MeuFinanceiro.Application.Reconciliation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MeuFinanceiro.Api.Controllers;

/// <summary>Tela Faturas (docs/screens/faturas).</summary>
[ApiController]
[Authorize]
[Route("api/v1/card-invoices")]
public class CardInvoicesController : ControllerBase
{
    private readonly IInvoiceService _invoiceService;
    private readonly ICardReconciliationService _reconciliationService;

    public CardInvoicesController(IInvoiceService invoiceService, ICardReconciliationService reconciliationService)
    {
        _invoiceService = invoiceService;
        _reconciliationService = reconciliationService;
    }

    /// <summary>Exclui uma fatura importada por engano (bloqueado se houver conciliação aceita).</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _reconciliationService.DeleteInvoiceAsync(id, ct);
        return NoContent();
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<CardInvoiceListItemResponse>>> GetAll([FromQuery] CardInvoiceFilter filter, CancellationToken ct)
        => Ok(await _invoiceService.GetAllAsync(filter, ct));
}
