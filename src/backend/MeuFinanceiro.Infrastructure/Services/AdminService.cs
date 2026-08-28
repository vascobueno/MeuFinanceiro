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

    public async Task<IReadOnlyList<AdminHouseholdResponse>> GetHouseholdsAsync(CancellationToken ct = default)
    {
        var households = await _db.Households.AsNoTracking().OrderBy(h => h.CreatedAt).ToListAsync(ct);
        var members = await _db.HouseholdMembers.AsNoTracking().ToListAsync(ct);

        var userIds = members.Select(m => m.UserId).Distinct().ToList();
        var users = await _userManager.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, ct);

        var result = new List<AdminHouseholdResponse>(households.Count);
        foreach (var household in households)
        {
            var householdMembers = members.Where(m => m.HouseholdId == household.Id).ToList();
            var owner = householdMembers.FirstOrDefault(m => m.Role == HouseholdRole.Owner);
            var ownerUser = owner is not null ? users.GetValueOrDefault(owner.UserId) : null;

            result.Add(new AdminHouseholdResponse(
                household.Id,
                household.Name,
                ownerUser?.Name ?? "—",
                ownerUser?.Email ?? "—",
                householdMembers.Count,
                household.CreatedAt));
        }

        return result;
    }

    public async Task<IReadOnlyList<AdminMemberResponse>> GetAllMembersAsync(CancellationToken ct = default)
    {
        var households = await _db.Households.AsNoTracking().ToDictionaryAsync(h => h.Id, ct);
        var members = await _db.HouseholdMembers.AsNoTracking().ToListAsync(ct);

        var userIds = members.Select(m => m.UserId).Distinct().ToList();
        var users = await _userManager.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, ct);

        return members
            .Select(m => new AdminMemberResponse(
                m.HouseholdId,
                households.GetValueOrDefault(m.HouseholdId)?.Name ?? "—",
                m.UserId,
                users.GetValueOrDefault(m.UserId)?.Name ?? string.Empty,
                users.GetValueOrDefault(m.UserId)?.Email ?? string.Empty,
                m.Role,
                m.AccessProfile))
            .ToList();
    }
}
