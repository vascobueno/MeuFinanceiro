using MeuFinanceiro.Application.Common.Exceptions;
using MeuFinanceiro.Application.Common.Interfaces;
using MeuFinanceiro.Application.Grupos;
using MeuFinanceiro.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MeuFinanceiro.Api.Controllers;

/// <summary>
/// Gestão do grupo do usuário autenticado. Ações que alteram membros são restritas ao
/// Owner — checado aqui no controller (não dentro de IGrupoService, que também é
/// reaproveitado pelo AdminController operando em qualquer grupo).
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/grupos")]
public class GruposController : ControllerBase
{
    private readonly IGrupoService _grupoService;
    private readonly ICurrentUserService _currentUser;

    public GruposController(IGrupoService grupoService, ICurrentUserService currentUser)
    {
        _grupoService = grupoService;
        _currentUser = currentUser;
    }

    [HttpGet("me")]
    public async Task<ActionResult<GrupoResponse>> GetMine(CancellationToken ct)
    {
        if (!_currentUser.HasFullAccess)
            throw new ForbiddenException("Seu perfil de acesso não permite ver esta página.");

        return Ok(await _grupoService.GetAsync(_currentUser.GrupoId, ct));
    }

    [HttpPost("me/members")]
    public async Task<ActionResult<GrupoMemberResponse>> AddMember(AddMemberRequest request, CancellationToken ct)
    {
        EnsureOwner();
        return Ok(await _grupoService.AddMemberAsync(_currentUser.GrupoId, request, ct));
    }

    [HttpPost("me/members/new")]
    public async Task<ActionResult<GrupoMemberResponse>> CreateMember(CreateMemberRequest request, CancellationToken ct)
    {
        EnsureOwner();
        return Ok(await _grupoService.CreateMemberAsync(_currentUser.GrupoId, request, ct));
    }

    [HttpDelete("me/members/{userId:guid}")]
    public async Task<IActionResult> RemoveMember(Guid userId, CancellationToken ct)
    {
        EnsureOwner();
        await _grupoService.RemoveMemberAsync(_currentUser.GrupoId, userId, ct);
        return NoContent();
    }

    [HttpPut("me/members/{userId:guid}/access-profile")]
    public async Task<IActionResult> UpdateMemberProfile(Guid userId, UpdateMemberAccessProfileRequest request, CancellationToken ct)
    {
        EnsureOwner();
        await _grupoService.UpdateMemberProfileAsync(_currentUser.GrupoId, userId, request.AccessProfile, ct);
        return NoContent();
    }

    private void EnsureOwner()
    {
        if (_currentUser.GrupoRole != GrupoRole.Owner)
            throw new ForbiddenException("Só o Owner do grupo pode gerenciar membros.");
    }
}
