using MeuFinanceiro.Application.Common.Exceptions;
using MeuFinanceiro.Application.Common.Interfaces;
using MeuFinanceiro.Application.Households;
using MeuFinanceiro.Domain.Entities;
using MeuFinanceiro.Domain.Enums;
using MeuFinanceiro.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MeuFinanceiro.Infrastructure.Services;

/// <summary>
/// Precisa de UserManager (buscar convidado por e-mail), por isso vive em Infrastructure —
/// mesmo motivo de AuthService.
/// </summary>
public class HouseholdService : IHouseholdService
{
    private readonly IApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public HouseholdService(IApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<Guid> CreateForNewUserAsync(Guid userId, string userName, CancellationToken ct = default)
    {
        var household = new Household { Name = $"Família de {userName}" };
        _db.Households.Add(household);

        _db.HouseholdMembers.Add(new HouseholdMember
        {
            HouseholdId = household.Id,
            UserId = userId,
            Role = HouseholdRole.Owner
        });

        await _db.SaveChangesAsync(ct);
        return household.Id;
    }

    public async Task<HouseholdResponse> GetAsync(Guid householdId, CancellationToken ct = default)
    {
        var household = await _db.Households.AsNoTracking().FirstOrDefaultAsync(h => h.Id == householdId, ct)
            ?? throw new NotFoundException(nameof(Household), householdId);

        var members = await _db.HouseholdMembers.AsNoTracking().Where(m => m.HouseholdId == householdId).ToListAsync(ct);
        var memberResponses = await ToMemberResponsesAsync(members, ct);

        return new HouseholdResponse(household.Id, household.Name, memberResponses);
    }

    public async Task<HouseholdMemberResponse> AddMemberAsync(Guid householdId, AddMemberRequest request, CancellationToken ct = default)
    {
        var invitee = await _userManager.FindByEmailAsync(request.Email)
            ?? throw new BusinessRuleException("usuario-nao-encontrado", "Não existe um usuário cadastrado com este e-mail. Peça para a pessoa se cadastrar primeiro.");

        var inviteeMembership = await _db.HouseholdMembers.FirstOrDefaultAsync(m => m.UserId == invitee.Id, ct)
            ?? throw new InvalidOperationException($"Usuário {invitee.Id} não possui household associado.");

        if (inviteeMembership.HouseholdId == householdId)
            throw new BusinessRuleException("usuario-ja-membro", "Este usuário já faz parte deste household.");

        // Só permite mover o convidado se o household dele ainda estiver intocado — evita
        // perda de dados silenciosa (mesclagem de dados é uma feature à parte, não pedida).
        var inviteeHouseholdMemberCount = await _db.HouseholdMembers.CountAsync(m => m.HouseholdId == inviteeMembership.HouseholdId, ct);
        if (inviteeHouseholdMemberCount > 1 || await HasAnyDataAsync(inviteeMembership.HouseholdId, ct))
            throw new BusinessRuleException("household-nao-vazio", "Este usuário já possui dados ou outros membros em seu próprio household e não pode ser movido.");

        var oldHousehold = await _db.Households.FirstAsync(h => h.Id == inviteeMembership.HouseholdId, ct);

        inviteeMembership.HouseholdId = householdId;
        inviteeMembership.Role = request.Role;
        _db.Households.Remove(oldHousehold);

        await _db.SaveChangesAsync(ct);

        return new HouseholdMemberResponse(invitee.Id, invitee.Name, invitee.Email!, inviteeMembership.Role, inviteeMembership.AccessProfile);
    }

    public async Task RemoveMemberAsync(Guid householdId, Guid userId, CancellationToken ct = default)
    {
        var membership = await _db.HouseholdMembers.FirstOrDefaultAsync(m => m.HouseholdId == householdId && m.UserId == userId, ct)
            ?? throw new NotFoundException(nameof(HouseholdMember), userId);

        if (membership.Role == HouseholdRole.Owner)
        {
            var ownerCount = await _db.HouseholdMembers.CountAsync(m => m.HouseholdId == householdId && m.Role == HouseholdRole.Owner, ct);
            if (ownerCount <= 1)
                throw new BusinessRuleException("ultimo-owner", "Não é possível remover o último Owner do household.");
        }

        // O removido nunca fica sem household — ganha um novo, vazio, do qual é Owner.
        var removedUser = await _userManager.FindByIdAsync(userId.ToString());
        var newHousehold = new Household { Name = $"Família de {removedUser?.Name ?? "novo espaço"}" };
        _db.Households.Add(newHousehold);

        membership.HouseholdId = newHousehold.Id;
        membership.Role = HouseholdRole.Owner;

        await _db.SaveChangesAsync(ct);
    }

    private async Task<List<HouseholdMemberResponse>> ToMemberResponsesAsync(List<HouseholdMember> members, CancellationToken ct)
    {
        var userIds = members.Select(m => m.UserId).ToList();
        var users = await _userManager.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, ct);

        return members
            .Select(m => new HouseholdMemberResponse(
                m.UserId,
                users.GetValueOrDefault(m.UserId)?.Name ?? string.Empty,
                users.GetValueOrDefault(m.UserId)?.Email ?? string.Empty,
                m.Role,
                m.AccessProfile))
            .ToList();
    }

    public async Task UpdateMemberProfileAsync(Guid householdId, Guid userId, HouseholdAccessProfile profile, CancellationToken ct = default)
    {
        var membership = await _db.HouseholdMembers.FirstOrDefaultAsync(m => m.HouseholdId == householdId && m.UserId == userId, ct)
            ?? throw new NotFoundException(nameof(HouseholdMember), userId);

        if (membership.Role == HouseholdRole.Owner)
            throw new BusinessRuleException("owner-acesso-completo", "O Owner sempre tem acesso completo e não pode ser rebaixado.");

        membership.AccessProfile = profile;
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Verifica as 12 tabelas de dados do household — usado pela regra de "convite só se intocado".</summary>
    private async Task<bool> HasAnyDataAsync(Guid householdId, CancellationToken ct)
    {
        return await _db.Accounts.AnyAsync(x => x.HouseholdId == householdId, ct)
            || await _db.CreditCards.AnyAsync(x => x.HouseholdId == householdId, ct)
            || await _db.Categories.AnyAsync(x => x.HouseholdId == householdId, ct)
            || await _db.Merchants.AnyAsync(x => x.HouseholdId == householdId, ct)
            || await _db.FinancialTransactions.AnyAsync(x => x.HouseholdId == householdId, ct)
            || await _db.RecurringTransactions.AnyAsync(x => x.HouseholdId == householdId, ct)
            || await _db.InstallmentPlans.AnyAsync(x => x.HouseholdId == householdId, ct)
            || await _db.BankStatements.AnyAsync(x => x.HouseholdId == householdId, ct)
            || await _db.CardInvoices.AnyAsync(x => x.HouseholdId == householdId, ct)
            || await _db.Reconciliations.AnyAsync(x => x.HouseholdId == householdId, ct)
            || await _db.InvestmentAccounts.AnyAsync(x => x.HouseholdId == householdId, ct)
            || await _db.ImportFiles.AnyAsync(x => x.HouseholdId == householdId, ct);
    }
}
