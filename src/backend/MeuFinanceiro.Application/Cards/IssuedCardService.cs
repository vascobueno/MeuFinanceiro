using MeuFinanceiro.Application.Common.Exceptions;
using MeuFinanceiro.Application.Common.Interfaces;
using MeuFinanceiro.Domain.Entities;
using MeuFinanceiro.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MeuFinanceiro.Application.Cards;

public interface IIssuedCardService
{
    Task<IReadOnlyList<IssuedCardResponse>> GetByGroupAsync(Guid creditCardId, CancellationToken ct = default);
    Task<IssuedCardResponse> CreateAsync(Guid creditCardId, IssuedCardRequest request, CancellationToken ct = default);
    Task<IssuedCardResponse> UpdateAsync(Guid id, IssuedCardRequest request, CancellationToken ct = default);
    Task<IssuedCardResponse> ReplaceAsync(Guid id, IssuedCardRequest request, CancellationToken ct = default);
    Task<IssuedCardResponse> CancelAsync(Guid id, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

/// <summary>
/// Cartões de um grupo (docs/04). Não há regra de "um titular só": o mesmo grupo pode ter dois
/// cartões de titular ativos (o banco emite um novo e o antigo continua valendo até vencer).
/// </summary>
public class IssuedCardService : IIssuedCardService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public IssuedCardService(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<IssuedCardResponse>> GetByGroupAsync(Guid creditCardId, CancellationToken ct = default)
    {
        var group = await FindGroupAsync(creditCardId, ct);
        var cards = await _db.IssuedCards.AsNoTracking()
            .Where(c => c.GrupoId == _currentUser.GrupoId && c.CreditCardId == creditCardId)
            .OrderBy(c => c.Status).ThenBy(c => c.Kind).ThenBy(c => c.LastFour)
            .ToListAsync(ct);
        return cards.Select(c => ToResponse(c, group.Brand)).ToList();
    }

    public async Task<IssuedCardResponse> CreateAsync(Guid creditCardId, IssuedCardRequest request, CancellationToken ct = default)
    {
        var group = await FindGroupAsync(creditCardId, ct);
        var data = Normalize(request);
        await EnsureUniqueActiveFinalAsync(data.LastFour, exceptId: null, ct);

        var card = new IssuedCard
        {
            GrupoId = _currentUser.GrupoId,
            CreditCardId = group.Id,
            CreatedBy = _currentUser.UserId,
        };
        Apply(card, data);
        _db.IssuedCards.Add(card);
        await _db.SaveChangesAsync(ct);
        return ToResponse(card, group.Brand);
    }

    public async Task<IssuedCardResponse> UpdateAsync(Guid id, IssuedCardRequest request, CancellationToken ct = default)
    {
        var card = await FindOwnedAsync(id, ct);
        var data = Normalize(request);
        if (card.Status == IssuedCardStatus.Ativo) await EnsureUniqueActiveFinalAsync(data.LastFour, card.Id, ct);

        Apply(card, data);
        card.UpdatedAt = DateTime.UtcNow;
        card.UpdatedBy = _currentUser.UserId;
        await _db.SaveChangesAsync(ct);
        return ToResponse(card, card.CreditCard!.Brand);
    }

    /// <summary>
    /// Troca um cartão por outro no mesmo grupo (renovação, perda, roubo): o antigo vira
    /// Substituído e aponta o novo. O novo pode ter o mesmo final — a renovação costuma manter o
    /// número e mudar só a validade.
    /// </summary>
    public async Task<IssuedCardResponse> ReplaceAsync(Guid id, IssuedCardRequest request, CancellationToken ct = default)
    {
        var old = await FindOwnedAsync(id, ct);
        if (old.Status != IssuedCardStatus.Ativo)
            throw new BusinessRuleException("cartao-nao-ativo", "Só um cartão ativo pode ser substituído.");

        var data = Normalize(request);
        // O antigo deixa de ser ativo nesta operação, então não conta para a unicidade.
        await EnsureUniqueActiveFinalAsync(data.LastFour, old.Id, ct);

        // Em dois passos: o índice único de final entre os ATIVOS veria dois ativos com o mesmo
        // final se o novo entrasse antes de o antigo sair. Tudo o que pode ser recusado já foi
        // validado acima.
        old.Status = IssuedCardStatus.Substituido;
        old.UpdatedAt = DateTime.UtcNow;
        old.UpdatedBy = _currentUser.UserId;
        await _db.SaveChangesAsync(ct);

        var replacement = new IssuedCard
        {
            GrupoId = _currentUser.GrupoId,
            CreditCardId = old.CreditCardId,
            CreatedBy = _currentUser.UserId,
        };
        Apply(replacement, data);
        _db.IssuedCards.Add(replacement);
        old.ReplacedById = replacement.Id;
        await _db.SaveChangesAsync(ct);

        return ToResponse(replacement, old.CreditCard!.Brand);
    }

    public async Task<IssuedCardResponse> CancelAsync(Guid id, CancellationToken ct = default)
    {
        var card = await FindOwnedAsync(id, ct);
        card.Status = IssuedCardStatus.Cancelado;
        card.UpdatedAt = DateTime.UtcNow;
        card.UpdatedBy = _currentUser.UserId;
        await _db.SaveChangesAsync(ct);
        return ToResponse(card, card.CreditCard!.Brand);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var card = await FindOwnedAsync(id, ct);

        // Quem foi substituído por este deixa de apontar para ele (a FK é Restrict).
        var replaced = await _db.IssuedCards.Where(c => c.ReplacedById == id).ToListAsync(ct);
        foreach (var c in replaced) c.ReplacedById = null;

        _db.IssuedCards.Remove(card);
        await _db.SaveChangesAsync(ct);
    }

    // ---------------- regras ----------------

    private sealed record CardData(IssuedCardKind Kind, string LastFour, string? FirstDigits, int? Month, int? Year, string? Holder);

    private static CardData Normalize(IssuedCardRequest r)
    {
        if (!Enum.IsDefined(r.Kind))
            throw new BusinessRuleException("tipo-de-cartao-invalido", "Escolha titular, adicional ou virtual.");

        var last = Digits(r.LastFour);
        var first = Digits(r.FirstDigits);

        // O número completo não entra por nenhum dos dois campos.
        if (last.Length > 4 || first.Length > 6)
            throw new BusinessRuleException("numero-completo-nao-guardado",
                "O número completo do cartão não é guardado: informe só os 6 primeiros dígitos (opcional) e os 4 últimos.");
        if (last.Length != 4)
            throw new BusinessRuleException("final-invalido", "Informe os 4 últimos dígitos do cartão.");
        if (first.Length is > 0 and < 4)
            throw new BusinessRuleException("inicio-invalido", "Os primeiros dígitos são de 4 a 6.");

        int? month = r.ExpiryMonth, year = r.ExpiryYear;
        if (month is null != year is null)
            throw new BusinessRuleException("validade-incompleta", "Informe mês e ano da validade — ou nenhum dos dois.");
        if (year is >= 0 and < 100) year += 2000;
        if (month is < 1 or > 12 || year is < 2000 or > 2099)
            throw new BusinessRuleException("validade-invalida", "Validade inválida: mês de 1 a 12 e ano com 2 ou 4 dígitos.");

        var holder = string.IsNullOrWhiteSpace(r.HolderName) ? null : r.HolderName.Trim();
        if (holder is { Length: > 60 })
            throw new BusinessRuleException("nome-muito-longo", "O nome impresso tem no máximo 60 caracteres.");

        return new CardData(r.Kind, last, first.Length == 0 ? null : first, month, year, holder);
    }

    private static void Apply(IssuedCard card, CardData d)
    {
        card.Kind = d.Kind;
        card.LastFour = d.LastFour;
        card.FirstDigits = d.FirstDigits;
        card.ExpiryMonth = d.Month;
        card.ExpiryYear = d.Year;
        card.HolderName = d.Holder;
    }

    /// <summary>
    /// O final identifica o cartão na fatura ("final 1234"), então dois cartões ativos do grupo não
    /// podem ter o mesmo. Substituídos e cancelados não contam: a renovação mantém o número.
    /// </summary>
    private async Task EnsureUniqueActiveFinalAsync(string lastFour, Guid? exceptId, CancellationToken ct)
    {
        var other = await _db.IssuedCards.AsNoTracking()
            .Where(c => c.GrupoId == _currentUser.GrupoId && c.Status == IssuedCardStatus.Ativo
                        && c.LastFour == lastFour && c.Id != exceptId)
            .Select(c => c.CreditCard!.Name)
            .FirstOrDefaultAsync(ct);
        if (other is not null)
            throw new BusinessRuleException("final-duplicado",
                $"Já existe um cartão ativo com final {lastFour}, em \"{other}\". Substitua ou cancele aquele antes.");
    }

    private static string Digits(string? value) => value is null ? "" : new string(value.Where(char.IsDigit).ToArray());

    private IssuedCardResponse ToResponse(IssuedCard c, CardBrand? groupBrand)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        // Vale até o último dia do mês impresso.
        var expired = c.ExpiryMonth is { } m && c.ExpiryYear is { } y && new DateOnly(y, m, 1).AddMonths(1) <= today;
        var suggested = CardBrands.SuggestFromDigits(c.FirstDigits);
        return new IssuedCardResponse(c.Id, c.CreditCardId, c.Kind, c.FirstDigits, c.LastFour, c.ExpiryMonth, c.ExpiryYear,
            c.HolderName, c.Status, expired, c.ReplacedById, suggested?.ToString(),
            suggested is not null && groupBrand is not null && suggested != groupBrand);
    }

    private async Task<CreditCard> FindGroupAsync(Guid id, CancellationToken ct)
    {
        var card = await _db.CreditCards.FirstOrDefaultAsync(c => c.Id == id, ct) ?? throw new NotFoundException(nameof(CreditCard), id);
        if (card.GrupoId != _currentUser.GrupoId) throw new ForbiddenException();
        return card;
    }

    private async Task<IssuedCard> FindOwnedAsync(Guid id, CancellationToken ct)
    {
        var card = await _db.IssuedCards.Include(c => c.CreditCard).FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new NotFoundException(nameof(IssuedCard), id);
        if (card.GrupoId != _currentUser.GrupoId) throw new ForbiddenException();
        return card;
    }
}
