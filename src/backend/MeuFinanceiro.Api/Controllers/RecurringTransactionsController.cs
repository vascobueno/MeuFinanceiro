using MeuFinanceiro.Application.Recurring;
using MeuFinanceiro.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MeuFinanceiro.Api.Controllers;

/// <summary>Tela Recorrência (docs/screens/recorrencia).</summary>
[ApiController]
[Authorize]
[Route("api/v1/recurring-transactions")]
public class RecurringTransactionsController : ControllerBase
{
    private readonly IRecurringTransactionService _recurringTransactionService;

    public RecurringTransactionsController(IRecurringTransactionService recurringTransactionService)
    {
        _recurringTransactionService = recurringTransactionService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RecurringTransactionResponse>>> GetAll([FromQuery] TransactionType? type, CancellationToken ct)
        => Ok(await _recurringTransactionService.GetAllAsync(type, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RecurringTransactionResponse>> GetById(Guid id, CancellationToken ct)
        => Ok(await _recurringTransactionService.GetByIdAsync(id, ct));

    [HttpPost("{id:guid}/versions")]
    public async Task<ActionResult<RecurringTransactionResponse>> CreateVersion(Guid id, CreateRecurringVersionRequest request, CancellationToken ct)
        => Ok(await _recurringTransactionService.CreateVersionAsync(id, request, ct));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<RecurringTransactionResponse>> Update(Guid id, UpdateRecurringTransactionRequest request, CancellationToken ct)
        => Ok(await _recurringTransactionService.UpdateAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _recurringTransactionService.DeleteAsync(id, ct);
        return NoContent();
    }
}
