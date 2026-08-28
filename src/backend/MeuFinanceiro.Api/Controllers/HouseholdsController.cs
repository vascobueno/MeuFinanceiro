using MeuFinanceiro.Application.Common.Exceptions;
using MeuFinanceiro.Application.Common.Interfaces;
using MeuFinanceiro.Application.Households;
using MeuFinanceiro.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MeuFinanceiro.Api.Controllers;

/// <summary>
/// Gestão do household do usuário autenticado. Ações que alteram membros são restritas ao
/// Owner — checado aqui no controller (não dentro de IHouseholdService, que também é
/// reaproveitado pelo AdminController operando em qualquer household).
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/households")]
public class HouseholdsController : ControllerBase
{
    private readonly IHouseholdService _householdService;
    private readonly ICurrentUserService _currentUser;

    public HouseholdsController(IHouseholdService householdService, ICurrentUserService currentUser)
    {
        _householdService = householdService;
        _currentUser = currentUser;
    }

    [HttpGet("me")]
    public async Task<ActionResult<HouseholdResponse>> GetMine(CancellationToken ct)
    {
        if (!_currentUser.HasFullAccess)
            throw new ForbiddenException("Seu perfil de acesso não permite ver esta página.");

        return Ok(await _householdService.GetAsync(_currentUser.HouseholdId, ct));
    }

    [HttpPost("me/members")]
    public async Task<ActionResult<HouseholdMemberResponse>> AddMember(AddMemberRequest request, CancellationToken ct)
    {
        EnsureOwner();
        return Ok(await _householdService.AddMemberAsync(_currentUser.HouseholdId, request, ct));
    }

    [HttpDelete("me/members/{userId:guid}")]
    public async Task<IActionResult> RemoveMember(Guid userId, CancellationToken ct)
    {
        EnsureOwner();
        await _householdService.RemoveMemberAsync(_currentUser.HouseholdId, userId, ct);
        return NoContent();
    }

    [HttpPut("me/members/{userId:guid}/access-profile")]
    public async Task<IActionResult> UpdateMemberProfile(Guid userId, UpdateMemberAccessProfileRequest request, CancellationToken ct)
    {
        EnsureOwner();
        await _householdService.UpdateMemberProfileAsync(_currentUser.HouseholdId, userId, request.AccessProfile, ct);
        return NoContent();
    }

    private void EnsureOwner()
    {
        if (_currentUser.HouseholdRole != HouseholdRole.Owner)
            throw new ForbiddenException("Só o Owner do household pode gerenciar membros.");
    }
}
