using MeuFinanceiro.Application.Common.Exceptions;
using MeuFinanceiro.Application.Common.Interfaces;
using MeuFinanceiro.Domain.Entities;
using MeuFinanceiro.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MeuFinanceiro.Application.Cards;

public interface IIssuedCardService
{
    Task<IReadOnlyList<IssuedCardResponse>> GetByGroupAsync(Guid creditCardId, CancellationToken ct = default);
    Task<IReadOnlyList<IssuedCardResponse>> GetByAccountAsync(Guid accountId, CancellationToken ct = default);
    Task<IssuedCardResponse> CreateAsync(Guid creditCardId, IssuedCardRequest request, CancellationToken ct = default);
    Task<IssuedCardResponse> CreateDebitAsync(Guid accountId, IssuedCardRequest request, CancellationToken ct = default);
    Task<IssuedCardResponse> UpdateAsync(Guid id, IssuedCardRequest request, CancellationToken ct = default);
    Task<IssuedCardResponse> ReplaceAsync(Guid id, IssuedCardRequest request, CancellationToken ct = default);
    Task<IssuedCardResponse> CancelAsync(Guid id, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

/// <summary>
/// Cartões (docs/04). Um cartão tem uma ou duas funções: crédito (está num grupo de cartões, a
/// fatura), débito (está ligado a uma conta) ou as duas — o cartão "múltiplo". É um registro só,
/// então cancelar ou substituir o múltiplo vale para os dois lados.
///
/// Não há regra de "um titular só": o mesmo grupo pode ter dois cartões de titular ativos.
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
        await FindGroupAsync(creditCardId, ct);
        return await ListAsync(c => c.CreditCardId == creditCardId, ct);
    }

    /// <summary>Os cartões com débito nesta conta: os só de débito e os múltiplos.</summary>
    public async Task<IReadOnlyList<IssuedCardResponse>> GetByAccountAsync(Guid accountId, CancellationToken ct = default)
    {
        await FindAccountAsync(accountId, ct);
        return await ListAsync(c => c.AccountId == accountId, ct);
    }

    private async Task<IReadOnlyList<IssuedCardResponse>> ListAsync(
        System.Linq.Expressions.Expression<Func<IssuedCard, bool>> filter, CancellationToken ct)
    {
        var cards = await _db.IssuedCards.AsNoTracking()
            .Include(c => c.CreditCard).Include(c => c.Account)
            .Where(c => c.GrupoId == _currentUser.GrupoId)
            .Where(filter)
            .OrderBy(c => c.Status).ThenBy(c => c.Kind).ThenBy(c => c.LastFour)
            .ToListAsync(ct);
        return cards.Select(ToResponse).ToList();
    }

    /// <summary>Cartão de crédito do grupo; com <c>AccountId</c>, é múltiplo (débito nessa conta).</summary>
    public async Task<IssuedCardResponse> CreateAsync(Guid creditCardId, IssuedCardRequest request, CancellationToken ct = default)
    {
        var group = await FindGroupAsync(creditCardId, ct);
        var account = request.AccountId is { } accountId ? await FindAccountAsync(accountId, ct) : null;
        return await CreateCoreAsync(group, account, request, ct);
    }

    /// <summary>Cartão só de débito, ligado à conta, com bandeira própria.</summary>
    public async Task<IssuedCardResponse> CreateDebitAsync(Guid accountId, IssuedCardRequest request, CancellationToken ct = default)
    {
        var account = await FindAccountAsync(accountId, ct);
        return await CreateCoreAsync(null, account, request, ct);
    }

    private async Task<IssuedCardResponse> CreateCoreAsync(CreditCard? group, Account? account, IssuedCardRequest request, CancellationToken ct)
    {
        var data = Normalize(request);
        var brand = BrandFor(group, request.Brand);
        await EnsureUniqueActiveFinalAsync(data.LastFour, exceptId: null, ct);

        var card = new IssuedCard
        {
            GrupoId = _currentUser.GrupoId,
            CreditCardId = group?.Id,
            CreditCard = group,
            AccountId = account?.Id,
            Account = account,
            Brand = brand,
            CreatedBy = _currentUser.UserId,
        };
        Apply(card, data);
        _db.IssuedCards.Add(card);
        await _db.SaveChangesAsync(ct);
        return ToResponse(card);
    }

    /// <summary>
    /// Edita os dados do cartão e a função de débito: mudar <c>AccountId</c> liga o débito a outra
    /// conta, e nulo tira o débito — de um cartão de crédito. Um cartão só de débito não pode ficar
    /// sem conta (não teria função nenhuma); o grupo de crédito não muda por aqui.
    /// </summary>
    public async Task<IssuedCardResponse> UpdateAsync(Guid id, IssuedCardRequest request, CancellationToken ct = default)
    {
        var card = await FindOwnedAsync(id, ct);
        var data = Normalize(request);

        Account? account = null;
        if (request.AccountId is { } accountId) account = await FindAccountAsync(accountId, ct);
        if (account is null && card.CreditCardId is null)
            throw new BusinessRuleException("cartao-sem-funcao",
                "Um cartão só de débito precisa de uma conta. Para deixar de usá-lo, cancele o cartão.");

        if (card.Status == IssuedCardStatus.Ativo) await EnsureUniqueActiveFinalAsync(data.LastFour, card.Id, ct);

        Apply(card, data);
        card.AccountId = account?.Id;
        card.Account = account;
        card.Brand = BrandFor(card.CreditCard, request.Brand);
        card.UpdatedAt = DateTime.UtcNow;
        card.UpdatedBy = _currentUser.UserId;
        await _db.SaveChangesAsync(ct);
        return ToResponse(card);
    }

    /// <summary>
    /// Troca um cartão por outro (renovação, perda, roubo): o antigo vira Substituído e aponta o
    /// novo, que herda as funções do antigo — o mesmo grupo e a mesma conta. O novo pode ter o mesmo
    /// final: a renovação costuma manter o número e mudar só a validade.
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
            CreditCard = old.CreditCard,
            AccountId = old.AccountId,
            Account = old.Account,
            Brand = old.CreditCardId is null ? BrandFor(null, request.Brand) ?? old.Brand : null,
            CreatedBy = _currentUser.UserId,
        };
        Apply(replacement, data);
        _db.IssuedCards.Add(replacement);
        old.ReplacedById = replacement.Id;
        await _db.SaveChangesAsync(ct);

        return ToResponse(replacement);
    }

    /// <summary>Cancela o cartão — o múltiplo deixa de valer no crédito e no débito.</summary>
    public async Task<IssuedCardResponse> CancelAsync(Guid id, CancellationToken ct = default)
    {
        var card = await FindOwnedAsync(id, ct);
        card.Status = IssuedCardStatus.Cancelado;
        card.UpdatedAt = DateTime.UtcNow;
        card.UpdatedBy = _currentUser.UserId;
        await _db.SaveChangesAsync(ct);
        return ToResponse(card);
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

    /// <summary>
    /// A bandeira de um cartão de crédito (só ou múltiplo) é a do grupo — a fatura é uma só. Só o
    /// cartão só de débito tem bandeira própria, e ela pode ser de débito (Maestro, Visa Electron,
    /// Elo Débito) ou não.
    /// </summary>
    private static CardBrand? BrandFor(CreditCard? group, string? requested)
    {
        if (group is not null) return null;
        return CardBrands.ParseAny(requested);
    }

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
    /// O final identifica o cartão na fatura e no extrato, então dois cartões ativos do grupo de
    /// usuários não podem ter o mesmo. Substituídos e cancelados não contam: a renovação mantém o
    /// número. O múltiplo é um cartão só, então não conflita consigo mesmo nos dois lados.
    /// </summary>
    private async Task EnsureUniqueActiveFinalAsync(string lastFour, Guid? exceptId, CancellationToken ct)
    {
        var other = await _db.IssuedCards.AsNoTracking()
            .Where(c => c.GrupoId == _currentUser.GrupoId && c.Status == IssuedCardStatus.Ativo
                        && c.LastFour == lastFour && c.Id != exceptId)
            .Select(c => c.CreditCard != null ? c.CreditCard.Name : c.Account!.Name)
            .FirstOrDefaultAsync(ct);
        if (other is not null)
            throw new BusinessRuleException("final-duplicado",
                $"Já existe um cartão ativo com final {lastFour}, em \"{other}\". Substitua ou cancele aquele antes.");
    }

    private static string Digits(string? value) => value is null ? "" : new string(value.Where(char.IsDigit).ToArray());

    private static IssuedCardResponse ToResponse(IssuedCard c)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        // Vale até o último dia do mês impresso.
        var expired = c.ExpiryMonth is { } m && c.ExpiryYear is { } y && new DateOnly(y, m, 1).AddMonths(1) <= today;

        // Bandeira efetiva: a do grupo no crédito, a própria no só de débito.
        var effective = c.CreditCard?.Brand ?? c.Brand;
        var suggested = CardBrands.SuggestFromDigits(c.FirstDigits);
        var mismatch = suggested is not null && effective is not null && !CardBrands.SameFamily(suggested.Value, effective.Value);

        var function = (c.CreditCardId, c.AccountId) switch
        {
            (not null, not null) => IssuedCardFunction.Multiplo,
            (null, not null) => IssuedCardFunction.Debito,
            _ => IssuedCardFunction.Credito,
        };

        return new IssuedCardResponse(c.Id, c.CreditCardId, c.Kind, c.FirstDigits, c.LastFour, c.ExpiryMonth, c.ExpiryYear,
            c.HolderName, c.Status, expired, c.ReplacedById, suggested?.ToString(), mismatch,
            function, c.AccountId, c.Account?.Name, c.CreditCard?.Name, effective?.ToString());
    }

    private async Task<CreditCard> FindGroupAsync(Guid id, CancellationToken ct)
    {
        var card = await _db.CreditCards.FirstOrDefaultAsync(c => c.Id == id, ct) ?? throw new NotFoundException(nameof(CreditCard), id);
        if (card.GrupoId != _currentUser.GrupoId) throw new ForbiddenException();
        return card;
    }

    private async Task<Account> FindAccountAsync(Guid id, CancellationToken ct)
    {
        var account = await _db.Accounts.FirstOrDefaultAsync(a => a.Id == id, ct) ?? throw new NotFoundException(nameof(Account), id);
        if (account.GrupoId != _currentUser.GrupoId) throw new ForbiddenException();
        return account;
    }

    private async Task<IssuedCard> FindOwnedAsync(Guid id, CancellationToken ct)
    {
        var card = await _db.IssuedCards.Include(c => c.CreditCard).Include(c => c.Account).FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new NotFoundException(nameof(IssuedCard), id);
        if (card.GrupoId != _currentUser.GrupoId) throw new ForbiddenException();
        return card;
    }
}
