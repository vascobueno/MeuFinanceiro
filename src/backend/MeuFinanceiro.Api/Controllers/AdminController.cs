using MeuFinanceiro.Application.Admin;
using MeuFinanceiro.Application.Grupos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MeuFinanceiro.Api.Controllers;

/// <summary>
/// Visão de administrador do sistema: enxerga e gerencia todos os grupos, não só o
/// próprio. [Authorize(Roles = "Administrator")] a nível de classe já garante que só quem tem
/// a role faz qualquer coisa aqui — reaproveita o mesmo IGrupoService da Parte 1 pras
/// operações de membro, só que sem o filtro "é o meu grupo".
/// </summary>
[ApiController]
[Authorize(Roles = "Administrator")]
[Route("api/v1/admin")]
public class AdminController : ControllerBase
{
    private readonly IAdminService _adminService;
    private readonly IGrupoService _grupoService;

    public AdminController(IAdminService adminService, IGrupoService grupoService)
    {
        _adminService = adminService;
        _grupoService = grupoService;
    }

    [HttpGet("grupos")]
    public async Task<ActionResult<IReadOnlyList<AdminGrupoResponse>>> GetGrupos(CancellationToken ct)
        => Ok(await _adminService.GetGruposAsync(ct));

    [HttpGet("members")]
    public async Task<ActionResult<IReadOnlyList<AdminMemberResponse>>> GetAllMembers(CancellationToken ct)
        => Ok(await _adminService.GetAllMembersAsync(ct));

    [HttpGet("grupos/{grupoId:guid}/members")]
    public async Task<ActionResult<GrupoResponse>> GetMembers(Guid grupoId, CancellationToken ct)
        => Ok(await _grupoService.GetAsync(grupoId, ct));

    [HttpPost("grupos/{grupoId:guid}/members")]
    public async Task<ActionResult<GrupoMemberResponse>> AddMember(Guid grupoId, AddMemberRequest request, CancellationToken ct)
        => Ok(await _grupoService.AddMemberAsync(grupoId, request, ct));

    [HttpPost("grupos/{grupoId:guid}/members/new")]
    public async Task<ActionResult<GrupoMemberResponse>> CreateMember(Guid grupoId, CreateMemberRequest request, CancellationToken ct)
        => Ok(await _grupoService.CreateMemberAsync(grupoId, request, ct));

    [HttpDelete("grupos/{grupoId:guid}/members/{userId:guid}")]
    public async Task<IActionResult> RemoveMember(Guid grupoId, Guid userId, CancellationToken ct)
    {
        await _grupoService.RemoveMemberAsync(grupoId, userId, ct);
        return NoContent();
    }

    [HttpPut("grupos/{grupoId:guid}/members/{userId:guid}/access-profile")]
    public async Task<IActionResult> UpdateMemberProfile(Guid grupoId, Guid userId, UpdateMemberAccessProfileRequest request, CancellationToken ct)
    {
        await _grupoService.UpdateMemberProfileAsync(grupoId, userId, request.AccessProfile, ct);
        return NoContent();
    }
}
