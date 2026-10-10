using MeuFinanceiro.Application.Cards;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MeuFinanceiro.Api.Controllers;

/// <summary>Cartões de um grupo: titular, adicionais e virtuais (docs/04, docs/13-api.md).</summary>
[ApiController]
[Authorize]
[Route("api/v1")]
public class IssuedCardsController : ControllerBase
{
    private readonly IIssuedCardService _service;

    public IssuedCardsController(IIssuedCardService service) => _service = service;

    [HttpGet("cards/{cardId:guid}/issued-cards")]
    public async Task<ActionResult<IReadOnlyList<IssuedCardResponse>>> GetByGroup(Guid cardId, CancellationToken ct)
        => Ok(await _service.GetByGroupAsync(cardId, ct));

    [HttpPost("cards/{cardId:guid}/issued-cards")]
    public async Task<ActionResult<IssuedCardResponse>> Create(Guid cardId, IssuedCardRequest request, CancellationToken ct)
    {
        var result = await _service.CreateAsync(cardId, request, ct);
        return Created($"/api/v1/issued-cards/{result.Id}", result);
    }

    /// <summary>Cartões com débito nesta conta: os só de débito e os múltiplos.</summary>
    [HttpGet("accounts/{accountId:guid}/debit-cards")]
    public async Task<ActionResult<IReadOnlyList<IssuedCardResponse>>> GetByAccount(Guid accountId, CancellationToken ct)
        => Ok(await _service.GetByAccountAsync(accountId, ct));

    /// <summary>Cartão só de débito, com bandeira própria.</summary>
    [HttpPost("accounts/{accountId:guid}/debit-cards")]
    public async Task<ActionResult<IssuedCardResponse>> CreateDebit(Guid accountId, IssuedCardRequest request, CancellationToken ct)
    {
        var result = await _service.CreateDebitAsync(accountId, request, ct);
        return Created($"/api/v1/issued-cards/{result.Id}", result);
    }

    [HttpPut("issued-cards/{id:guid}")]
    public async Task<ActionResult<IssuedCardResponse>> Update(Guid id, IssuedCardRequest request, CancellationToken ct)
        => Ok(await _service.UpdateAsync(id, request, ct));

    /// <summary>Troca o cartão por um novo no mesmo grupo; devolve o novo.</summary>
    [HttpPost("issued-cards/{id:guid}/replace")]
    public async Task<ActionResult<IssuedCardResponse>> Replace(Guid id, IssuedCardRequest request, CancellationToken ct)
        => Ok(await _service.ReplaceAsync(id, request, ct));

    [HttpPost("issued-cards/{id:guid}/cancel")]
    public async Task<ActionResult<IssuedCardResponse>> Cancel(Guid id, CancellationToken ct)
        => Ok(await _service.CancelAsync(id, ct));

    [HttpDelete("issued-cards/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }
}
