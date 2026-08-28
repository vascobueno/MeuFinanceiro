using MeuFinanceiro.Application.Admin;
using MeuFinanceiro.Application.Households;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MeuFinanceiro.Api.Controllers;

/// <summary>
/// Visão de administrador do sistema: enxerga e gerencia todos os households, não só o
/// próprio. [Authorize(Roles = "Administrator")] a nível de classe já garante que só quem tem
/// a role faz qualquer coisa aqui — reaproveita o mesmo IHouseholdService da Parte 1 pras
/// operações de membro, só que sem o filtro "é o meu household".
/// </summary>
[ApiController]
[Authorize(Roles = "Administrator")]
[Route("api/v1/admin")]
public class AdminController : ControllerBase
{
    private readonly IAdminService _adminService;
    private readonly IHouseholdService _householdService;

    public AdminController(IAdminService adminService, IHouseholdService householdService)
    {
        _adminService = adminService;
        _householdService = householdService;
    }

    [HttpGet("households")]
    public async Task<ActionResult<IReadOnlyList<AdminHouseholdResponse>>> GetHouseholds(CancellationToken ct)
        => Ok(await _adminService.GetHouseholdsAsync(ct));

    [HttpGet("members")]
    public async Task<ActionResult<IReadOnlyList<AdminMemberResponse>>> GetAllMembers(CancellationToken ct)
        => Ok(await _adminService.GetAllMembersAsync(ct));

    [HttpGet("households/{householdId:guid}/members")]
    public async Task<ActionResult<HouseholdResponse>> GetMembers(Guid householdId, CancellationToken ct)
        => Ok(await _householdService.GetAsync(householdId, ct));

    [HttpPost("households/{householdId:guid}/members")]
    public async Task<ActionResult<HouseholdMemberResponse>> AddMember(Guid householdId, AddMemberRequest request, CancellationToken ct)
        => Ok(await _householdService.AddMemberAsync(householdId, request, ct));

    [HttpDelete("households/{householdId:guid}/members/{userId:guid}")]
    public async Task<IActionResult> RemoveMember(Guid householdId, Guid userId, CancellationToken ct)
    {
        await _householdService.RemoveMemberAsync(householdId, userId, ct);
        return NoContent();
    }

    [HttpPut("households/{householdId:guid}/members/{userId:guid}/access-profile")]
    public async Task<IActionResult> UpdateMemberProfile(Guid householdId, Guid userId, UpdateMemberAccessProfileRequest request, CancellationToken ct)
    {
        await _householdService.UpdateMemberProfileAsync(householdId, userId, request.AccessProfile, ct);
        return NoContent();
    }
}
