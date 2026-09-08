using MeuFinanceiro.Application.Common;
using MeuFinanceiro.Application.Invoices;
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

    public CardInvoicesController(IInvoiceService invoiceService)
    {
        _invoiceService = invoiceService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<CardInvoiceListItemResponse>>> GetAll([FromQuery] CardInvoiceFilter filter, CancellationToken ct)
        => Ok(await _invoiceService.GetAllAsync(filter, ct));
}
