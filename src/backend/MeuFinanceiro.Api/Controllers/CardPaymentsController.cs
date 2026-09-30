using MeuFinanceiro.Application.Cards;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MeuFinanceiro.Api.Controllers;

/// <summary>
/// Pagamentos de fatura de cartão (docs/05 — Pagamento de cartão). A fatura em si é lida em
/// GET /api/v1/cards/{id}/invoice, porque é um sub-recurso do cartão e não um recurso próprio:
/// ela é calculada a partir dos lançamentos, não armazenada.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/card-payments")]
public class CardPaymentsController : ControllerBase
{
    private readonly ICardPaymentService _cardPaymentService;

    public CardPaymentsController(ICardPaymentService cardPaymentService)
    {
        _cardPaymentService = cardPaymentService;
    }

    [HttpPost]
    public async Task<ActionResult<CardPaymentResponse>> Create(CreateCardPaymentRequest request, CancellationToken ct)
        => Ok(await _cardPaymentService.CreateAsync(request, ct));

    /// <summary>Permite trocar a conta de onde saiu o pagamento — inclusive para "outra forma".</summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CardPaymentResponse>> Update(Guid id, UpdateCardPaymentRequest request, CancellationToken ct)
        => Ok(await _cardPaymentService.UpdateAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _cardPaymentService.DeleteAsync(id, ct);
        return NoContent();
    }
}
