using MeuFinanceiro.Application.Projection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MeuFinanceiro.Api.Controllers;

/// <summary>GET /api/v1/projection?date=2026-12-31 — docs/11-projecao-financeira.md.</summary>
[ApiController]
[Authorize]
[Route("api/v1/projection")]
public class ProjectionController : ControllerBase
{
    private readonly IProjectionService _service;

    public ProjectionController(IProjectionService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<ProjectionResponse>> Get(
        [FromQuery] DateOnly date,
        [FromQuery] int? maxTimelineEntries,
        CancellationToken ct)
        => Ok(await _service.GetProjectionAsync(date, maxTimelineEntries, ct));
}
