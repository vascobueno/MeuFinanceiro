using MeuFinanceiro.Application.Accounts;
using MeuFinanceiro.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MeuFinanceiro.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/accounts")]
public class AccountsController : ControllerBase
{
    private readonly IAccountService _accountService;

    public AccountsController(IAccountService accountService)
    {
        _accountService = accountService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<AccountResponse>>> GetAll([FromQuery] PagedRequest request, CancellationToken ct)
        => Ok(await _accountService.GetAllAsync(request, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AccountResponse>> GetById(Guid id, CancellationToken ct)
        => Ok(await _accountService.GetByIdAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<AccountResponse>> Create(CreateAccountRequest request, CancellationToken ct)
    {
        var result = await _accountService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AccountResponse>> Update(Guid id, UpdateAccountRequest request, CancellationToken ct)
        => Ok(await _accountService.UpdateAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _accountService.DeleteAsync(id, ct);
        return NoContent();
    }
}
