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

    public async Task<IReadOnlyDictionary<string, Guid>> GetOrCreateManyAsync(
        IReadOnlyCollection<string> descriptions,
        CancellationToken ct = default)
    {
        // Quantas vezes cada descrição aparece no lote — UsageCount tem de refletir o total
        // importado, não apenas "+1", que é o que sairia chamando a versão de item único em laço.
        var occurrences = descriptions
            .Select(d => d.Trim())
            .Where(d => !string.IsNullOrEmpty(d))
            .GroupBy(d => d)
            .ToDictionary(g => g.Key, g => g.Count());

        if (occurrences.Count == 0) return new Dictionary<string, Guid>();

        var names = occurrences.Keys.ToList();
        var existing = await _db.Merchants
            .Where(m => m.GrupoId == _currentUser.GrupoId && names.Contains(m.Name))
            .ToListAsync(ct);

        var result = new Dictionary<string, Guid>(occurrences.Count);
        var now = DateTime.UtcNow;

        foreach (var merchant in existing)
        {
            merchant.UsageCount += occurrences[merchant.Name];
            merchant.LastUsedAt = now;
            merchant.UpdatedAt = now;
            merchant.UpdatedBy = _currentUser.UserId;
            result[merchant.Name] = merchant.Id;
        }

        foreach (var (name, count) in occurrences)
        {
            if (result.ContainsKey(name)) continue;

            // O Id já vem preenchido pela entidade (Guid.NewGuid no construtor), então o chamador
            // pode referenciá-lo antes do commit.
            var merchant = new Merchant
            {
                GrupoId = _currentUser.GrupoId,
                Name = name,
                UsageCount = count,
                LastUsedAt = now,
                CreatedBy = _currentUser.UserId
            };
            _db.Merchants.Add(merchant);
            result[name] = merchant.Id;
        }

        return result;
    }
}
