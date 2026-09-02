using MeuFinanceiro.Application.Common.Interfaces;
using MeuFinanceiro.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MeuFinanceiro.Application.Merchants;

public class MerchantService : IMerchantService
{
    private const int MaxSuggestionsHardLimit = 15;

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public MerchantService(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<MerchantSuggestionResponse>> GetSuggestionsAsync(string prefix, int maxResults, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(prefix))
            return Array.Empty<MerchantSuggestionResponse>();

        var take = Math.Clamp(maxResults, 1, MaxSuggestionsHardLimit);

        var normalizedPrefix = prefix.ToLower();
        var merchants = await _db.Merchants
            .AsNoTracking()
            // ToLower() dos dois lados em vez de StartsWith direto: no SQL Server o LIKE
            // gerado por StartsWith já era case-insensitive por causa da collation padrão; no
            // Postgres LIKE é case-sensitive por padrão, o que quebraria a regra 18 ("ama"
            // encontra "Amazon"). Evita EF.Functions.ILike (específico do Npgsql) para não
            // criar dependência do provider de banco na camada Application.
            .Where(m => m.GrupoId == _currentUser.GrupoId && m.Name.ToLower().StartsWith(normalizedPrefix))
            .OrderByDescending(m => m.UsageCount)
            .ThenByDescending(m => m.LastUsedAt)
            .Take(take)
            .ToListAsync(ct);

        return merchants.Select(m => new MerchantSuggestionResponse(m.Id, m.Name, m.DefaultCategoryId)).ToList();
    }

    public async Task<Guid> GetOrCreateAndTouchAsync(string description, CancellationToken ct = default)
    {
        var normalized = description.Trim();

        var merchant = await _db.Merchants.FirstOrDefaultAsync(
            m => m.GrupoId == _currentUser.GrupoId && m.Name == normalized, ct);

        if (merchant is null)
        {
            merchant = new Merchant
            {
                GrupoId = _currentUser.GrupoId,
                Name = normalized,
                UsageCount = 1,
                LastUsedAt = DateTime.UtcNow,
                CreatedBy = _currentUser.UserId
            };
            _db.Merchants.Add(merchant);
        }
        else
        {
            merchant.UsageCount++;
            merchant.LastUsedAt = DateTime.UtcNow;
            merchant.UpdatedAt = DateTime.UtcNow;
            merchant.UpdatedBy = _currentUser.UserId;
        }

        await _db.SaveChangesAsync(ct);
        return merchant.Id;
    }
}
