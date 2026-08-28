using MeuFinanceiro.Application.Auth;
using MeuFinanceiro.Application.Common.Exceptions;
using MeuFinanceiro.Application.Common.Interfaces;
using MeuFinanceiro.Application.Households;
using MeuFinanceiro.Domain.Enums;
using MeuFinanceiro.Infrastructure.Identity;
using MeuFinanceiro.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MeuFinanceiro.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITokenService _tokenService;
    private readonly IHouseholdService _householdService;
    private readonly IAuditService _auditService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AppDbContext _db;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        ITokenService tokenService,
        IHouseholdService householdService,
        IAuditService auditService,
        IHttpContextAccessor httpContextAccessor,
        AppDbContext db)
    {
        _userManager = userManager;
        _tokenService = tokenService;
        _householdService = householdService;
        _auditService = auditService;
        _httpContextAccessor = httpContextAccessor;
        _db = db;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var existing = await _userManager.FindByEmailAsync(request.Email);
        if (existing is not null)
            throw new BusinessRuleException("email-em-uso", "Já existe um usuário cadastrado com este e-mail.");

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            Name = request.Name
        };

        async Task CreateUserAndHouseholdAsync()
        {
            var result = await _userManager.CreateAsync(user, request.Password);
            if (!result.Succeeded)
                throw new BusinessRuleException("cadastro-invalido", string.Join(" ", result.Errors.Select(e => e.Description)));

            await _householdService.CreateForNewUserAsync(user.Id, user.Name, ct);
        }

        // Transação explícita: um usuário nunca deve existir sem household. Sem isso, uma
        // falha ao criar o Household depois de UserManager.CreateAsync ter sucesso deixaria um
        // usuário órfão. CreateExecutionStrategy é necessário porque o Npgsql pode ter retry-
        // on-failure habilitado, que exige que a transação seja aberta dentro da strategy.
        // IsRelational() só é falso no provider InMemory usado pelos testes de integração, que
        // não suporta transação nenhuma — nesse caso as duas operações rodam sem transação
        // (é exatamente o que a suíte de testes já espera).
        if (_db.Database.IsRelational())
        {
            var strategy = _db.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _db.Database.BeginTransactionAsync(ct);
                await CreateUserAndHouseholdAsync();
                await transaction.CommitAsync(ct);
            });
        }
        else
        {
            await CreateUserAndHouseholdAsync();
        }

        return await IssueTokensAsync(user, ct);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null || !await _userManager.CheckPasswordAsync(user, request.Password))
        {
            // Household só é conhecido se o usuário existir (senha errada) — e-mail inexistente
            // não tem household nenhum pra associar ao evento.
            Guid? householdId = null;
            if (user is not null)
                householdId = (await _db.HouseholdMembers.AsNoTracking().FirstOrDefaultAsync(m => m.UserId == user.Id, ct))?.HouseholdId;

            await _auditService.LogAccessAsync("LoginFailed", user?.Id, householdId, GetClientIp(), GetUserAgent(), ct);
            throw new UnauthorizedAccessException("E-mail ou senha inválidos.");
        }

        var response = await IssueTokensAsync(user, ct);
        await _auditService.LogAccessAsync("Login", user.Id, response.User.HouseholdId, GetClientIp(), GetUserAgent(), ct);
        return response;
    }

    private string? GetClientIp() => _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
    private string? GetUserAgent() => _httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString();

    public async Task<AuthResponse> RefreshAsync(RefreshTokenRequest request, CancellationToken ct = default)
    {
        var hash = _tokenService.HashRefreshToken(request.RefreshToken);
        var stored = await _db.RefreshTokens.Include(r => r.User)
            .FirstOrDefaultAsync(r => r.TokenHash == hash, ct);

        if (stored is null || !stored.IsActive || stored.User is null)
            throw new UnauthorizedAccessException("Refresh token inválido ou expirado.");

        stored.RevokedAt = DateTime.UtcNow;
        var response = await IssueTokensAsync(stored.User, ct);
        return response;
    }

    public async Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        var hash = _tokenService.HashRefreshToken(refreshToken);
        var stored = await _db.RefreshTokens.FirstOrDefaultAsync(r => r.TokenHash == hash, ct);
        if (stored is not null)
        {
            stored.RevokedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
    }

    private async Task<AuthResponse> IssueTokensAsync(ApplicationUser user, CancellationToken ct)
    {
        // Busca no momento da emissão (não fica em cache) — mudanças de papel/household
        // refletem em até 15 min (TTL do access token), igual a qualquer outro dado do token.
        var membership = await _db.HouseholdMembers.AsNoTracking().FirstOrDefaultAsync(m => m.UserId == user.Id, ct)
            ?? throw new InvalidOperationException($"Usuário {user.Id} não possui household associado.");

        var roles = await _userManager.GetRolesAsync(user);

        var (accessToken, accessExpiresAt) = _tokenService.GenerateAccessToken(
            user.Id, user.Email!, user.Name, membership.HouseholdId, membership.Role, membership.AccessProfile, roles);
        var (refreshToken, refreshExpiresAt) = _tokenService.GenerateRefreshToken();

        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = _tokenService.HashRefreshToken(refreshToken),
            ExpiresAt = refreshExpiresAt
        });
        await _db.SaveChangesAsync(ct);

        var effectiveAccessProfile = membership.Role == HouseholdRole.Owner ? HouseholdAccessProfile.Completo : membership.AccessProfile;

        return new AuthResponse(accessToken, accessExpiresAt, refreshToken, refreshExpiresAt,
            new UserResponse(user.Id, user.Name, user.Email!, membership.HouseholdId, membership.Role, effectiveAccessProfile, roles.Contains(IdentitySeeder.AdministratorRole)));
    }
}
