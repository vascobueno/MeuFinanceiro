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

    /// <summary>Lê o texto colado da fatura e devolve os itens normalizados, sem gravar nada.</summary>
    [HttpPost("card-invoices/parse")]
    public async Task<ActionResult<Application.Reconciliation.Import.ParsedCardInvoiceResponse>> Parse(
        Application.Reconciliation.Import.ParseCardInvoiceRequest request, CancellationToken ct)
        => Ok(await _service.ParseAsync(request, ct));

    /// <summary>
    /// Lê a fatura em PDF (Santander, Pan), sem gravar nada. A senha vem no formulário, abre o
    /// arquivo e é descartada: não vai para log, auditoria nem resposta. Até 10 MB.
    /// </summary>
    [HttpPost("card-invoices/parse-pdf")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<Application.Reconciliation.Import.ParsedCardInvoiceResponse>> ParsePdf(
        IFormFile file, [FromForm] string? password, [FromForm] DateOnly? closingDate, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new ProblemDetails { Title = "Envie o PDF da fatura." });

        using var memory = new MemoryStream();
        await file.CopyToAsync(memory, ct);
        return Ok(await _service.ParsePdfAsync(memory.ToArray(), password, closingDate, ct));
    }

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

    /// <summary>Cria no cartão o lançamento de um item sem par (anuidade, IOF…) e o concilia.</summary>
    [HttpPost("reconciliations/card/{id:guid}/create-transaction")]
    public async Task<ActionResult<CardReconciliationResponse>> CreateTransaction(Guid id, CreateFromInvoiceItemRequest? request, CancellationToken ct)
        => Ok(await _service.CreateTransactionFromItemAsync(id, request ?? new CreateFromInvoiceItemRequest(), ct));

    [HttpPost("reconciliations/card/{id:guid}/undo")]
    public async Task<IActionResult> Undo(Guid id, CancellationToken ct)
    {
        await _service.UndoAsync(id, ct);
        return NoContent();
    }
}
