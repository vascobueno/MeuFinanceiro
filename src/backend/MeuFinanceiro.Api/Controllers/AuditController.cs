using MeuFinanceiro.Application.Common;
using MeuFinanceiro.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MeuFinanceiro.Api.Controllers;

/// <summary>
/// Relatório de auditoria (acessos + alterações). Owner/Member vê só o audit log do próprio
/// household; Administrator vê de todos (visibilidade resolvida dentro do AuditService).
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/audit")]
public class AuditController : ControllerBase
{
    private readonly IAuditService _auditService;

    public AuditController(IAuditService auditService)
    {
        _auditService = auditService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<AuditLogResponse>>> GetAll([FromQuery] AuditLogFilter filter, CancellationToken ct)
        => Ok(await _auditService.GetAllAsync(filter, ct));
}
