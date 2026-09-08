using MeuFinanceiro.Application.Admin;
using MeuFinanceiro.Application.Common.Interfaces;
using MeuFinanceiro.Domain.Enums;
using MeuFinanceiro.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MeuFinanceiro.Infrastructure.Services;

public class AdminService : IAdminService
{
    private readonly IApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public AdminService(IApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<IReadOnlyList<AdminGrupoResponse>> GetGruposAsync(CancellationToken ct = default)
    {
        var grupos = await _db.Grupos.AsNoTracking().OrderBy(h => h.CreatedAt).ToListAsync(ct);
        var members = await _db.GrupoMembers.AsNoTracking().ToListAsync(ct);

        var userIds = members.Select(m => m.UserId).Distinct().ToList();
        var users = await _userManager.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, ct);

        var result = new List<AdminGrupoResponse>(grupos.Count);
        foreach (var grupo in grupos)
        {
            var grupoMembers = members.Where(m => m.GrupoId == grupo.Id).ToList();
            var owner = grupoMembers.FirstOrDefault(m => m.Role == GrupoRole.Owner);
            var ownerUser = owner is not null ? users.GetValueOrDefault(owner.UserId) : null;

            result.Add(new AdminGrupoResponse(
                grupo.Id,
                grupo.Name,
                ownerUser?.Name ?? "—",
                ownerUser?.Email ?? "—",
                grupoMembers.Count,
                grupo.CreatedAt));
        }

        return result;
    }

    public async Task<IReadOnlyList<AdminMemberResponse>> GetAllMembersAsync(CancellationToken ct = default)
    {
        var grupos = await _db.Grupos.AsNoTracking().ToDictionaryAsync(h => h.Id, ct);
        var members = await _db.GrupoMembers.AsNoTracking().ToListAsync(ct);

        var userIds = members.Select(m => m.UserId).Distinct().ToList();
        var users = await _userManager.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, ct);

        return members
            .Select(m => new AdminMemberResponse(
                m.GrupoId,
                grupos.GetValueOrDefault(m.GrupoId)?.Name ?? "—",
                m.UserId,
                users.GetValueOrDefault(m.UserId)?.Name ?? string.Empty,
                users.GetValueOrDefault(m.UserId)?.Email ?? string.Empty,
                m.Role,
                m.AccessProfile))
            .ToList();
    }
}
