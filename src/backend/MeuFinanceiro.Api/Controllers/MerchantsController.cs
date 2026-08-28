using MeuFinanceiro.Application.Merchants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MeuFinanceiro.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/merchants")]
public class MerchantsController : ControllerBase
{
    private readonly IMerchantService _merchantService;

    public MerchantsController(IMerchantService merchantService)
    {
        _merchantService = merchantService;
    }

    /// <summary>
    /// GET /api/v1/merchants/suggestions?prefix=ama — RF005. Debounce (~300ms) e limite
    /// (10-15) ficam no front-end; o backend limita defensivamente também.
    /// </summary>
    [HttpGet("suggestions")]
    public async Task<ActionResult<IReadOnlyList<MerchantSuggestionResponse>>> GetSuggestions(
        [FromQuery] string prefix, [FromQuery] int maxResults = 15, CancellationToken ct = default)
        => Ok(await _merchantService.GetSuggestionsAsync(prefix, maxResults, ct));
}
