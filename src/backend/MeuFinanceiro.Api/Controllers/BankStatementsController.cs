using MeuFinanceiro.Application.Common;
using MeuFinanceiro.Application.Reconciliation;
using MeuFinanceiro.Application.Statements;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MeuFinanceiro.Api.Controllers;

/// <summary>
/// Tela Extratos. A importação de extrato continua em BankReconciliationController
/// (POST bank-statements/import); aqui só se lista o que já foi importado.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/bank-statements")]
public class BankStatementsController : ControllerBase
{
    private readonly IStatementService _statementService;
    private readonly IBankReconciliationService _reconciliationService;

    public BankStatementsController(IStatementService statementService, IBankReconciliationService reconciliationService)
    {
        _statementService = statementService;
        _reconciliationService = reconciliationService;
    }

    /// <summary>Exclui um extrato importado por engano (bloqueado se houver conciliação aceita).</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _reconciliationService.DeleteStatementAsync(id, ct);
        return NoContent();
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<BankStatementListItemResponse>>> GetAll([FromQuery] BankStatementFilter filter, CancellationToken ct)
        => Ok(await _statementService.GetAllAsync(filter, ct));
}
