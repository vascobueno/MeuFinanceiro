using MeuFinanceiro.Application.Common;
using MeuFinanceiro.Application.Common.Exceptions;
using MeuFinanceiro.Application.Common.Interfaces;
using MeuFinanceiro.Domain.Entities;
using MeuFinanceiro.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MeuFinanceiro.Application.Recurring;

/// <summary>
/// Tela Recorrência (docs/screens/recorrencia). Regra 13: alteração de valor cria uma nova
/// versão vigente a partir de uma data; versões e ocorrências anteriores nunca são
/// reescritas — a ocorrência já materializada como FinancialTransaction mantém seu valor
/// original, e a projeção (ProjectionService) já escolhe a versão certa por EffectiveFrom.
/// </summary>
public class RecurringTransactionService : IRecurringTransactionService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public RecurringTransactionService(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<RecurringTransactionResponse>> GetAllAsync(RecurringTransactionFilter filter, CancellationToken ct = default)
    {
        var query = _db.RecurringTransactions
            .AsNoTracking()
            .Include(r => r.Versions)
            .Where(r => r.GrupoId == _currentUser.GrupoId);

        if (filter.Type is not null) query = query.Where(r => r.Type == filter.Type);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var normalizedSearch = filter.Search.ToLower();
            query = query.Where(r => r.Description.ToLower().Contains(normalizedSearch));
        }

        query = filter.SortBy?.ToLowerInvariant() switch
        {
            "frequency" => filter.IsDescending ? query.OrderByDescending(r => r.Frequency) : query.OrderBy(r => r.Frequency),
            "startdate" => filter.IsDescending ? query.OrderByDescending(r => r.StartDate) : query.OrderBy(r => r.StartDate),
            "isactive" => filter.IsDescending ? query.OrderByDescending(r => r.IsActive) : query.OrderBy(r => r.IsActive),
            _ => filter.IsDescending ? query.OrderByDescending(r => r.Description) : query.OrderBy(r => r.Description)
        };

        var totalItems = await query.CountAsync(ct);
        var recurring = await query.Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).ToListAsync(ct);

        // As próximas ocorrências são calculadas só para a página: antes o endpoint devolvia todas
        // as recorrências de uma vez, cada uma com 3 ocorrências — medido em 2,2 MB com 5 mil.
        var items = await ToResponsesAsync(recurring, ct);
        return PagedResult<RecurringTransactionResponse>.Create(items.ToList(), filter.Page, filter.PageSize, totalItems);
    }

    public async Task<IReadOnlyList<RecurringExpenseReportItem>> GetReportAsync(Domain.Enums.TransactionType? type = null, CancellationToken ct = default)
    {
        var query = _db.RecurringTransactions
            .AsNoTracking()
            .Include(r => r.Versions)
            .Where(r => r.GrupoId == _currentUser.GrupoId && r.IsActive);

        if (type is not null) query = query.Where(r => r.Type == type);

        var recurring = await query.OrderBy(r => r.Description).ToListAsync(ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        return recurring
            .Select(r => new RecurringExpenseReportItem(
                r.Id,
                r.Description,
                r.Frequency,
                VersionAt(r, today).Amount,
                RecurrenceOccurrenceCalculator.NextOccurrences(r, today, 1).Select(d => (DateOnly?)d).FirstOrDefault()))
            .ToList();
    }

    public async Task<RecurringTransactionResponse> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await ToResponseWithNamesAsync(await FindOwnedAsync(id, ct), ct);

    public async Task<RecurringTransactionResponse> CreateVersionAsync(Guid id, CreateRecurringVersionRequest request, CancellationToken ct = default)
    {
        var recurring = await FindOwnedAsync(id, ct);

        if (request.Amount <= 0)
            throw new BusinessRuleException("recorrencia-valor-invalido", "O novo valor deve ser maior que zero.");

        var latestEffectiveFrom = recurring.Versions.Max(v => v.EffectiveFrom);
        if (request.EffectiveFrom <= latestEffectiveFrom)
            throw new BusinessRuleException(
                "recorrencia-data-invalida",
                $"A nova data de vigência deve ser posterior à versão vigente atual ({latestEffectiveFrom:dd/MM/yyyy}).");

        var newVersion = new RecurringTransactionVersion
        {
            RecurringTransactionId = recurring.Id,
            EffectiveFrom = request.EffectiveFrom,
            Amount = request.Amount,
            CreatedBy = _currentUser.UserId
        };
        _db.RecurringTransactionVersions.Add(newVersion);
        recurring.Versions.Add(newVersion);

        await _db.SaveChangesAsync(ct);
        return await ToResponseWithNamesAsync(recurring, ct);
    }

    public async Task<RecurringTransactionResponse> UpdateAsync(Guid id, UpdateRecurringTransactionRequest request, CancellationToken ct = default)
    {
        var recurring = await FindOwnedAsync(id, ct);
        TransactionOriginRules.Validate(request.AccountId, request.CreditCardId, recurring.Type);

        recurring.Description = request.Description;
        recurring.AccountId = request.AccountId;
        recurring.CreditCardId = request.CreditCardId;
        recurring.CategoryId = request.CategoryId;
        recurring.EndDate = request.EndDate;
        recurring.IsActive = request.IsActive;
        recurring.UpdatedAt = DateTime.UtcNow;
        recurring.UpdatedBy = _currentUser.UserId;

        await _db.SaveChangesAsync(ct);
        return await ToResponseWithNamesAsync(recurring, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var recurring = await FindOwnedAsync(id, ct);

        // A FK FinancialTransaction.RecurringTransactionId é Restrict (AppDbContext), então as
        // ocorrências já materializadas precisam ser tratadas antes. O critério é o status: o
        // que já foi conferido contra o extrato (Reconciled) é dinheiro que de fato se moveu e
        // continua valendo como lançamento avulso — só perde o vínculo. O que ainda era
        // previsão (Pending/Confirmed) some junto com a regra que o gerou.
        var linked = await _db.FinancialTransactions.Where(t => t.RecurringTransactionId == id).ToListAsync(ct);

        foreach (var transaction in linked)
        {
            if (transaction.Status == TransactionStatus.Reconciled)
                transaction.RecurringTransactionId = null;
            else
                _db.FinancialTransactions.Remove(transaction);
        }

        // As versões caem por cascade (configurado em AppDbContext).
        _db.RecurringTransactions.Remove(recurring);
        await _db.SaveChangesAsync(ct);
    }

    private async Task<RecurringTransaction> FindOwnedAsync(Guid id, CancellationToken ct)
    {
        var recurring = await _db.RecurringTransactions
            .Include(r => r.Versions)
            .FirstOrDefaultAsync(r => r.Id == id, ct) ?? throw new NotFoundException(nameof(RecurringTransaction), id);

        if (recurring.GrupoId != _currentUser.GrupoId) throw new ForbiddenException();
        return recurring;
    }

    /// <summary>Quantas ocorrências futuras mostrar na prévia da listagem (regra 13/docs/11).</summary>
    private const int NextOccurrencesPreviewCount = 3;

    /// <summary>
    /// RecurringTransaction guarda só o Id da conta/cartão (sem navegação mapeada), então os
    /// nomes são resolvidos à parte, numa query por lista em vez de uma por item.
    /// </summary>
    private async Task<IReadOnlyList<RecurringTransactionResponse>> ToResponsesAsync(IReadOnlyList<RecurringTransaction> recurring, CancellationToken ct)
    {
        var accountIds = recurring.Where(r => r.AccountId is not null).Select(r => r.AccountId!.Value).ToList();
        var cardIds = recurring.Where(r => r.CreditCardId is not null).Select(r => r.CreditCardId!.Value).ToList();

        var accountNames = await _db.Accounts.Where(a => accountIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, a => a.Name, ct);
        var cardNames = await _db.CreditCards.Where(c => cardIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name, ct);

        return recurring
            .Select(r => ToResponse(
                r,
                r.AccountId is Guid accId ? accountNames.GetValueOrDefault(accId) : null,
                r.CreditCardId is Guid cardId ? cardNames.GetValueOrDefault(cardId) : null))
            .ToList();
    }

    private async Task<RecurringTransactionResponse> ToResponseWithNamesAsync(RecurringTransaction recurring, CancellationToken ct) =>
        (await ToResponsesAsync(new[] { recurring }, ct))[0];

    private static RecurringTransactionResponse ToResponse(RecurringTransaction recurring, string? accountName = null, string? creditCardName = null)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var currentVersion = VersionAt(recurring, today);

        var nextOccurrences = recurring.IsActive
            ? RecurrenceOccurrenceCalculator.NextOccurrences(recurring, today, NextOccurrencesPreviewCount)
                .Select(date => new RecurringOccurrencePreview(date, VersionAt(recurring, date).Amount))
                .ToList()
            : new List<RecurringOccurrencePreview>();

        return new RecurringTransactionResponse(
            recurring.Id,
            recurring.Description,
            recurring.Type,
            currentVersion.Amount,
            recurring.Frequency,
            recurring.AccountId,
            accountName,
            recurring.CreditCardId,
            creditCardName,
            recurring.CategoryId,
            recurring.StartDate,
            recurring.EndDate,
            recurring.IsActive,
            nextOccurrences
        );
    }

    /// <summary>Versão vigente numa data (regra 13: sempre a mais recente com EffectiveFrom <= a data).</summary>
    private static RecurringTransactionVersion VersionAt(RecurringTransaction recurring, DateOnly date) =>
        recurring.Versions
            .Where(v => v.EffectiveFrom <= date)
            .OrderByDescending(v => v.EffectiveFrom)
            .FirstOrDefault() ?? recurring.Versions.OrderBy(v => v.EffectiveFrom).First();
}
