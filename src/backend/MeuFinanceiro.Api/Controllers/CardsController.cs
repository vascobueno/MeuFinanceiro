using MeuFinanceiro.Application.Cards;
using MeuFinanceiro.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MeuFinanceiro.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/cards")]
public class CardsController : ControllerBase
{
    private readonly ICardService _cardService;
    private readonly ICardInvoiceCycleService _invoiceCycleService;

    public CardsController(ICardService cardService, ICardInvoiceCycleService invoiceCycleService)
    {
        _cardService = cardService;
        _invoiceCycleService = invoiceCycleService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<CardResponse>>> GetAll([FromQuery] PagedRequest request, CancellationToken ct)
        => Ok(await _cardService.GetAllAsync(request, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CardResponse>> GetById(Guid id, CancellationToken ct)
        => Ok(await _cardService.GetByIdAsync(id, ct));

    /// <summary>
    /// Fatura do ciclo, calculada a partir dos lançamentos. `reference` é a data de fechamento do
    /// ciclo desejado; omitida, devolve o ciclo atual.
    /// </summary>
    [HttpGet("{id:guid}/invoice")]
    public async Task<ActionResult<CardInvoiceCycleResponse>> GetInvoiceCycle(
        Guid id, [FromQuery] DateOnly? reference, CancellationToken ct)
        => Ok(await _invoiceCycleService.GetAsync(id, reference, ct));

    [HttpPost]
    public async Task<ActionResult<CardResponse>> Create(CreateCardRequest request, CancellationToken ct)
    {
        var result = await _cardService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CardResponse>> Update(Guid id, UpdateCardRequest request, CancellationToken ct)
        => Ok(await _cardService.UpdateAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _cardService.DeleteAsync(id, ct);
        return NoContent();
    }
}
