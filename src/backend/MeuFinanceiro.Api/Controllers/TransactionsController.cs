using MeuFinanceiro.Application.Transactions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MeuFinanceiro.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/transactions")]
public class TransactionsController : ControllerBase
{
    private readonly ITransactionService _transactionService;

    public TransactionsController(ITransactionService transactionService)
    {
        _transactionService = transactionService;
    }

    /// <summary>Filtros/ordenação/paginação server-side (regra 19, docs/06-lancamentos.md).</summary>
    [HttpGet]
    public async Task<ActionResult> GetAll([FromQuery] TransactionFilter filter, CancellationToken ct)
        => Ok(await _transactionService.GetAllAsync(filter, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TransactionResponse>> GetById(Guid id, CancellationToken ct)
        => Ok(await _transactionService.GetByIdAsync(id, ct));

    /// <summary>
    /// Cria um lançamento simples, uma transferência, um plano de parcelamento ou uma
    /// recorrência, conforme os campos preenchidos em <see cref="CreateTransactionRequest"/>.
    /// Retorna a lista de lançamentos efetivamente criados (1 para lançamento simples,
    /// 2 para transferência, N para parcelamento).
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<IReadOnlyList<TransactionResponse>>> Create(CreateTransactionRequest request, CancellationToken ct)
        => Ok(await _transactionService.CreateAsync(request, ct));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TransactionResponse>> Update(Guid id, UpdateTransactionRequest request, CancellationToken ct)
        => Ok(await _transactionService.UpdateAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _transactionService.DeleteAsync(id, ct);
        return NoContent();
    }
}
