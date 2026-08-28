using MeuFinanceiro.Application.Common.Exceptions;
using MeuFinanceiro.Application.Common.Interfaces;
using MeuFinanceiro.Domain.Entities;
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

    public async Task<IReadOnlyList<RecurringTransactionResponse>> GetAllAsync(Domain.Enums.TransactionType? type = null, CancellationToken ct = default)
    {
        var query = _db.RecurringTransactions
            .AsNoTracking()
            .Include(r => r.Versions)
            .Where(r => r.HouseholdId == _currentUser.HouseholdId);

        if (type is not null) query = query.Where(r => r.Type == type);

        var recurring = await query.OrderBy(r => r.Description).ToListAsync(ct);

        // RecurringTransaction guarda só o Id da conta/cartão (sem navegação mapeada), então
        // os nomes são resolvidos à parte — mesmo padrão de outras telas que cruzam listas
        // carregadas separadamente pelo frontend, só que aqui é mais simples resolver no backend.
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

    public async Task<RecurringTransactionResponse> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        ToResponse(await FindOwnedAsync(id, ct));

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
        return ToResponse(recurring);
    }

    private async Task<RecurringTransaction> FindOwnedAsync(Guid id, CancellationToken ct)
    {
        var recurring = await _db.RecurringTransactions
            .Include(r => r.Versions)
            .FirstOrDefaultAsync(r => r.Id == id, ct) ?? throw new NotFoundException(nameof(RecurringTransaction), id);

        if (recurring.HouseholdId != _currentUser.HouseholdId) throw new ForbiddenException();
        return recurring;
    }

    /// <summary>Quantas ocorrências futuras mostrar na prévia da listagem (regra 13/docs/11).</summary>
    private const int NextOccurrencesPreviewCount = 3;

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
            accountName,
            creditCardName,
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
