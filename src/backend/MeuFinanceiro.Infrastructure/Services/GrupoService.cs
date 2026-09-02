using System.Security.Cryptography;
using MeuFinanceiro.Application.Common.Exceptions;
using MeuFinanceiro.Application.Common.Interfaces;
using MeuFinanceiro.Application.Grupos;
using MeuFinanceiro.Domain.Entities;
using MeuFinanceiro.Domain.Enums;
using MeuFinanceiro.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace MeuFinanceiro.Infrastructure.Services;

/// <summary>
/// Precisa de UserManager (buscar convidado por e-mail), por isso vive em Infrastructure —
/// mesmo motivo de AuthService.
/// </summary>
public class GrupoService : IGrupoService
{
    private readonly IApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailSender _emailSender;
    private readonly IConfiguration _configuration;

    public GrupoService(IApplicationDbContext db, UserManager<ApplicationUser> userManager, IEmailSender emailSender, IConfiguration configuration)
    {
        _db = db;
        _userManager = userManager;
        _emailSender = emailSender;
        _configuration = configuration;
    }

    public async Task<Guid> CreateForNewUserAsync(Guid userId, string userName, CancellationToken ct = default)
    {
        var grupo = new Grupo { Name = $"Família de {userName}" };
        _db.Grupos.Add(grupo);

        _db.GrupoMembers.Add(new GrupoMember
        {
            GrupoId = grupo.Id,
            UserId = userId,
            Role = GrupoRole.Owner
        });

        await _db.SaveChangesAsync(ct);
        return grupo.Id;
    }

    public async Task<GrupoResponse> GetAsync(Guid grupoId, CancellationToken ct = default)
    {
        var grupo = await _db.Grupos.AsNoTracking().FirstOrDefaultAsync(h => h.Id == grupoId, ct)
            ?? throw new NotFoundException(nameof(Grupo), grupoId);

        var members = await _db.GrupoMembers.AsNoTracking().Where(m => m.GrupoId == grupoId).ToListAsync(ct);
        var memberResponses = await ToMemberResponsesAsync(members, ct);

        return new GrupoResponse(grupo.Id, grupo.Name, memberResponses);
    }

    public async Task<GrupoMemberResponse> AddMemberAsync(Guid grupoId, AddMemberRequest request, CancellationToken ct = default)
    {
        var invitee = await _userManager.FindByEmailAsync(request.Email)
            ?? throw new BusinessRuleException("usuario-nao-encontrado", "Não existe um usuário cadastrado com este e-mail. Peça para a pessoa se cadastrar primeiro.");

        var inviteeMembership = await _db.GrupoMembers.FirstOrDefaultAsync(m => m.UserId == invitee.Id, ct)
            ?? throw new InvalidOperationException($"Usuário {invitee.Id} não possui grupo associado.");

        if (inviteeMembership.GrupoId == grupoId)
            throw new BusinessRuleException("usuario-ja-membro", "Este usuário já faz parte deste grupo.");

        // Só permite mover o convidado se o grupo dele ainda estiver intocado — evita
        // perda de dados silenciosa (mesclagem de dados é uma feature à parte, não pedida).
        var inviteeGrupoMemberCount = await _db.GrupoMembers.CountAsync(m => m.GrupoId == inviteeMembership.GrupoId, ct);
        if (inviteeGrupoMemberCount > 1 || await HasAnyDataAsync(inviteeMembership.GrupoId, ct))
            throw new BusinessRuleException("grupo-nao-vazio", "Este usuário já possui dados ou outros membros em seu próprio grupo e não pode ser movido.");

        var oldGrupo = await _db.Grupos.FirstAsync(h => h.Id == inviteeMembership.GrupoId, ct);

        inviteeMembership.GrupoId = grupoId;
        inviteeMembership.Role = request.Role;
        _db.Grupos.Remove(oldGrupo);

        await _db.SaveChangesAsync(ct);

        return new GrupoMemberResponse(invitee.Id, invitee.Name, invitee.Email!, inviteeMembership.Role, inviteeMembership.AccessProfile);
    }

    public async Task<GrupoMemberResponse> CreateMemberAsync(Guid grupoId, CreateMemberRequest request, CancellationToken ct = default)
    {
        if (await _userManager.FindByEmailAsync(request.Email) is not null)
            throw new BusinessRuleException("email-ja-cadastrado", "Já existe uma conta com este e-mail. Use \"Adicionar membro\" pra convidá-la, informando o e-mail dela.");
        if (await _userManager.FindByNameAsync(request.Username) is not null)
            throw new BusinessRuleException("usuario-em-uso", "Já existe uma conta com este nome de usuário.");

        var temporaryPassword = GenerateTemporaryPassword();
        var user = new ApplicationUser
        {
            UserName = request.Username,
            Email = request.Email,
            Name = request.Name,
            EmailConfirmed = false,
            MustChangePassword = true
        };
        var result = await _userManager.CreateAsync(user, temporaryPassword);
        if (!result.Succeeded)
            throw new BusinessRuleException("cadastro-invalido", string.Join(" ", result.Errors.Select(e => e.Description)));

        _db.GrupoMembers.Add(new GrupoMember { GrupoId = grupoId, UserId = user.Id, Role = request.Role, AccessProfile = GrupoAccessProfile.Completo });
        await _db.SaveChangesAsync(ct);

        var configuredFrontendBaseUrl = _configuration["App:FrontendBaseUrl"];
        var frontendBaseUrl = string.IsNullOrWhiteSpace(configuredFrontendBaseUrl) ? "http://localhost:4200" : configuredFrontendBaseUrl;

        var body = $"""
            <p>Olá, {user.Name}.</p>
            <p>Uma conta foi criada pra você no MeuFinanceiro.</p>
            <p>Nome de usuário: <b>{user.UserName}</b></p>
            <p>Senha temporária: <b>{temporaryPassword}</b></p>
            <p>Ao entrar pela primeira vez, você vai precisar trocar essa senha.</p>
            <p><a href="{frontendBaseUrl}/login">Entrar agora</a></p>
            """;
        await _emailSender.SendAsync(user.Email, "Sua conta no MeuFinanceiro", body, ct);

        return new GrupoMemberResponse(user.Id, user.Name, user.Email!, request.Role, GrupoAccessProfile.Completo);
    }

    private static string GenerateTemporaryPassword()
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnpqrstuvwxyz";
        const string digits = "23456789";
        const string symbols = "!@#$%&*";
        var all = upper + lower + digits + symbols;
        var bytes = RandomNumberGenerator.GetBytes(16);
        var chars = bytes.Select(b => all[b % all.Length]).ToArray();
        // Garante ao menos 1 de cada categoria (política do Identity), trocando as 4 primeiras posições.
        chars[0] = upper[RandomNumberGenerator.GetInt32(upper.Length)];
        chars[1] = lower[RandomNumberGenerator.GetInt32(lower.Length)];
        chars[2] = digits[RandomNumberGenerator.GetInt32(digits.Length)];
        chars[3] = symbols[RandomNumberGenerator.GetInt32(symbols.Length)];
        return new string(chars);
    }

    public async Task RemoveMemberAsync(Guid grupoId, Guid userId, CancellationToken ct = default)
    {
        var membership = await _db.GrupoMembers.FirstOrDefaultAsync(m => m.GrupoId == grupoId && m.UserId == userId, ct)
            ?? throw new NotFoundException(nameof(GrupoMember), userId);

        if (membership.Role == GrupoRole.Owner)
        {
            var ownerCount = await _db.GrupoMembers.CountAsync(m => m.GrupoId == grupoId && m.Role == GrupoRole.Owner, ct);
            if (ownerCount <= 1)
                throw new BusinessRuleException("ultimo-owner", "Não é possível remover o último Owner do grupo.");
        }

        // O removido nunca fica sem grupo — ganha um novo, vazio, do qual é Owner.
        var removedUser = await _userManager.FindByIdAsync(userId.ToString());
        var newGrupo = new Grupo { Name = $"Família de {removedUser?.Name ?? "novo espaço"}" };
        _db.Grupos.Add(newGrupo);

        membership.GrupoId = newGrupo.Id;
        membership.Role = GrupoRole.Owner;

        await _db.SaveChangesAsync(ct);
    }

    private async Task<List<GrupoMemberResponse>> ToMemberResponsesAsync(List<GrupoMember> members, CancellationToken ct)
    {
        var userIds = members.Select(m => m.UserId).ToList();
        var users = await _userManager.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, ct);

        return members
            .Select(m => new GrupoMemberResponse(
                m.UserId,
                users.GetValueOrDefault(m.UserId)?.Name ?? string.Empty,
                users.GetValueOrDefault(m.UserId)?.Email ?? string.Empty,
                m.Role,
                m.AccessProfile))
            .ToList();
    }

    public async Task UpdateMemberProfileAsync(Guid grupoId, Guid userId, GrupoAccessProfile profile, CancellationToken ct = default)
    {
        var membership = await _db.GrupoMembers.FirstOrDefaultAsync(m => m.GrupoId == grupoId && m.UserId == userId, ct)
            ?? throw new NotFoundException(nameof(GrupoMember), userId);

        if (membership.Role == GrupoRole.Owner)
            throw new BusinessRuleException("owner-acesso-completo", "O Owner sempre tem acesso completo e não pode ser rebaixado.");

        membership.AccessProfile = profile;
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Verifica as 12 tabelas de dados do grupo — usado pela regra de "convite só se intocado".</summary>
    private async Task<bool> HasAnyDataAsync(Guid grupoId, CancellationToken ct)
    {
        return await _db.Accounts.AnyAsync(x => x.GrupoId == grupoId, ct)
            || await _db.CreditCards.AnyAsync(x => x.GrupoId == grupoId, ct)
            || await _db.Categories.AnyAsync(x => x.GrupoId == grupoId, ct)
            || await _db.Merchants.AnyAsync(x => x.GrupoId == grupoId, ct)
            || await _db.FinancialTransactions.AnyAsync(x => x.GrupoId == grupoId, ct)
            || await _db.RecurringTransactions.AnyAsync(x => x.GrupoId == grupoId, ct)
            || await _db.InstallmentPlans.AnyAsync(x => x.GrupoId == grupoId, ct)
            || await _db.BankStatements.AnyAsync(x => x.GrupoId == grupoId, ct)
            || await _db.CardInvoices.AnyAsync(x => x.GrupoId == grupoId, ct)
            || await _db.Reconciliations.AnyAsync(x => x.GrupoId == grupoId, ct)
            || await _db.InvestmentAccounts.AnyAsync(x => x.GrupoId == grupoId, ct)
            || await _db.ImportFiles.AnyAsync(x => x.GrupoId == grupoId, ct);
    }
}
