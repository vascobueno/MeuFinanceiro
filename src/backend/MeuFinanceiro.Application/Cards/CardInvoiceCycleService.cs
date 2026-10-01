using MeuFinanceiro.Application.Common.Exceptions;
using MeuFinanceiro.Application.Common.Interfaces;
using MeuFinanceiro.Domain.Entities;
using MeuFinanceiro.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MeuFinanceiro.Application.Cards;

/// <summary>
/// Monta a fatura de um ciclo somando os lançamentos do cartão dentro da janela do ciclo
/// (CardCycleCalculator) e descontando os pagamentos alocados a ele.
///
/// Não confundir com InvoiceService/CardInvoice: aquilo é o PDF importado do banco, usado só para
/// conciliação, com o total vindo pronto no arquivo. Aqui o total é derivado do que o usuário
/// lançou — é o número que ele controla.
/// </summary>
public class CardInvoiceCycleService : ICardInvoiceCycleService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public CardInvoiceCycleService(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    public async Task<CardInvoiceCycleResponse> GetAsync(Guid cardId, DateOnly? reference, CancellationToken ct = default)
    {
        var card = await _db.CreditCards
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == cardId && c.GrupoId == _currentUser.GrupoId, ct)
            ?? throw new NotFoundException("Cartão", cardId);

        var baseCycle = ResolveCycle(card.ClosingDay, card.DueDay, reference);
        var cycleReference = ReferenceOf(baseCycle);
        var (cycle, closingOverridden, dueOverridden) = await ApplyOverridesAsync(card, baseCycle, cycleReference, ct);

        // Total do ciclo numa consulta agregada, nunca somando a lista paginada em memória.
        //
        // TODO-NÃO: não filtrar por Status. Os lançamentos nascem Pending (BuildTransaction fixa
        // isso), então excluir pendentes — como o saldo de conta faz — deixaria a fatura zerada
        // para praticamente todo mundo. No cartão, "pendente" significa não conferido, não
        // "ainda não gastei".
        var total = await _db.FinancialTransactions
            .AsNoTracking()
            .Where(t => t.CreditCardId == cardId
                     && t.GrupoId == _currentUser.GrupoId
                     && t.TransactionDate >= cycle.Start
                     && t.TransactionDate <= cycle.ClosingDate)
            .SumAsync(t => t.Direction == TransactionDirection.Debit ? t.Amount : -t.Amount, ct);

        var payments = await _db.CardPayments
            .AsNoTracking()
            .Where(p => p.CreditCardId == cardId
                     && p.GrupoId == _currentUser.GrupoId
                     && p.CycleReference == cycleReference)
            .OrderBy(p => p.PaymentDate)
            .Select(p => new CardPaymentResponse(
                p.Id, p.CreditCardId, p.CycleReference, p.PaymentDate, p.Amount,
                p.AccountId, p.Account!.Name, p.PaymentTransactionId))
            .ToListAsync(ct);

        var defaultDue = CardCycleCalculator.DueDateFor(baseCycle.ClosingDate, card.DueDay);
        var paid = payments.Sum(p => p.Amount);
        var outstanding = total - paid;   // pode ficar negativo: pagar a mais é crédito, não erro
        var isClosed = cycle.ClosingDate < Today;

        return new CardInvoiceCycleResponse(
            card.Id, card.Name,
            cycleReference,
            cycle.Start, cycle.ClosingDate, cycle.DueDate,
            total, paid, outstanding,
            isClosed,
            IsOverdue: isClosed && cycle.DueDate < Today && outstanding > 0,
            IsClosingOverridden: closingOverridden,
            DefaultClosingDate: baseCycle.ClosingDate,
            IsDueOverridden: dueOverridden,
            DefaultDueDate: defaultDue,
            payments);
    }

    public async Task<CardInvoiceCycleResponse> SetClosingAsync(
        Guid cardId, DateOnly reference, DateOnly closingDate, CancellationToken ct = default)
    {
        var (card, existing) = await PrepareOverrideAsync(cardId, reference, ct);

        // O fechamento tem de ficar DENTRO do próprio mês. Fora dele, dois ciclos se sobreporiam
        // (o mesmo lançamento entraria em duas faturas) ou se inverteriam (janela vazia) — e os
        // dois erram em silêncio, que é o pior jeito de errar dinheiro.
        if (closingDate.Year != reference.Year || closingDate.Month != reference.Month)
            throw new BusinessRuleException(
                "fechamento-fora-do-mes",
                $"O fechamento tem de ser uma data de {reference:MM/yyyy}.");

        // Com vencimento já ajustado, mover o fechamento para cima ou depois dele é recusado em vez
        // de empurrar o vencimento: o valor foi escolhido pelo usuário, e alterá-lo em silêncio é
        // pior que pedir para ajustar na ordem certa.
        if (existing?.DueDate is { } due && closingDate >= due)
            throw new BusinessRuleException(
                "fechamento-depois-do-vencimento",
                $"O fechamento ficaria em cima ou depois do vencimento ajustado ({due:dd/MM/yyyy}). Ajuste o vencimento primeiro.");

        await UpsertOverrideAsync(card, reference, existing, o => o.ClosingDate = closingDate, ct);
        return await GetAsync(cardId, reference, ct);
    }

    public async Task<CardInvoiceCycleResponse> ClearClosingAsync(
        Guid cardId, DateOnly reference, CancellationToken ct = default) =>
        await ClearOverrideAsync(cardId, reference, o => o.ClosingDate = null, ct);

    public async Task<CardInvoiceCycleResponse> SetDueAsync(
        Guid cardId, DateOnly reference, DateOnly dueDate, CancellationToken ct = default)
    {
        var (card, existing) = await PrepareOverrideAsync(cardId, reference, ct);

        // Validado contra o fechamento EFETIVO deste mês, que já pode estar ajustado.
        var (cycle, _, _) = await ApplyOverridesAsync(
            card, CardCycleCalculator.CycleForMonth(card.ClosingDay, card.DueDay, reference), reference, ct);

        if (dueDate <= cycle.ClosingDate)
            throw new BusinessRuleException(
                "vencimento-antes-do-fechamento",
                $"O vencimento tem de ser depois do fechamento ({cycle.ClosingDate:dd/MM/yyyy}).");

        // Limite frouxo, só contra erro de digitação (ano errado). Ao contrário do fechamento, o
        // vencimento não define a janela do ciclo — ele só aparece na tela e no cálculo de atraso.
        if (dueDate > cycle.ClosingDate.AddDays(MaxDueDaysAfterClosing))
            throw new BusinessRuleException(
                "vencimento-distante",
                $"O vencimento não pode passar de {MaxDueDaysAfterClosing} dias após o fechamento.");

        await UpsertOverrideAsync(card, reference, existing, o => o.DueDate = dueDate, ct);
        return await GetAsync(cardId, reference, ct);
    }

    public async Task<CardInvoiceCycleResponse> ClearDueAsync(
        Guid cardId, DateOnly reference, CancellationToken ct = default) =>
        await ClearOverrideAsync(cardId, reference, o => o.DueDate = null, ct);

    private const int MaxDueDaysAfterClosing = 90;

    private async Task<(CreditCard Card, CardCycleOverride? Existing)> PrepareOverrideAsync(
        Guid cardId, DateOnly reference, CancellationToken ct)
    {
        if (!_currentUser.CanWrite)
            throw new ForbiddenException("Perfil somente leitura não pode ajustar o calendário da fatura.");

        var card = await FindCardAsync(cardId, ct);
        EnsureReferenceIsMonth(reference);

        var existing = await _db.CardCycleOverrides
            .FirstOrDefaultAsync(o => o.CreditCardId == cardId
                                   && o.GrupoId == _currentUser.GrupoId
                                   && o.ReferenceMonth == reference, ct);

        return (card, existing);
    }

    private async Task UpsertOverrideAsync(
        CreditCard card, DateOnly reference, CardCycleOverride? existing,
        Action<CardCycleOverride> aplicar, CancellationToken ct)
    {
        if (existing is null)
        {
            var novo = new CardCycleOverride
            {
                GrupoId = _currentUser.GrupoId,
                CreditCardId = card.Id,
                ReferenceMonth = reference,
                CreatedBy = _currentUser.UserId,
            };
            aplicar(novo);
            _db.CardCycleOverrides.Add(novo);
        }
        else
        {
            aplicar(existing);
            existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedBy = _currentUser.UserId;
        }

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Limpa um dos dois ajustes. Quando o outro já era nulo, a linha inteira sai — linha com os
    /// dois nulos não diz nada e só acumularia na tabela.
    /// </summary>
    private async Task<CardInvoiceCycleResponse> ClearOverrideAsync(
        Guid cardId, DateOnly reference, Action<CardCycleOverride> limpar, CancellationToken ct)
    {
        var (_, existing) = await PrepareOverrideAsync(cardId, reference, ct);

        // Sem ajuste gravado já é o estado desejado — remover o que não existe não é erro.
        if (existing is not null)
        {
            limpar(existing);

            if (existing.ClosingDate is null && existing.DueDate is null)
                _db.CardCycleOverrides.Remove(existing);
            else
            {
                existing.UpdatedAt = DateTime.UtcNow;
                existing.UpdatedBy = _currentUser.UserId;
            }

            await _db.SaveChangesAsync(ct);
        }

        return await GetAsync(cardId, reference, ct);
    }

    private async Task<CreditCard> FindCardAsync(Guid cardId, CancellationToken ct) =>
        await _db.CreditCards.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == cardId && c.GrupoId == _currentUser.GrupoId, ct)
        ?? throw new NotFoundException("Cartão", cardId);

    private static void EnsureReferenceIsMonth(DateOnly reference)
    {
        if (reference.Day != 1)
            throw new BusinessRuleException(
                "referencia-invalida",
                $"A referência da fatura é o mês (dia 1), e {reference:dd/MM/yyyy} não é.");
    }

    /// <summary>
    /// Aplica as exceções de calendário (CardCycleOverride) ao ciclo calculado.
    ///
    /// Precisa do mês ANTERIOR também: o início do ciclo é o dia seguinte ao fechamento dele, e se
    /// aquele mês tiver ajuste, a janela deste muda junto. Os dois vêm numa consulta só.
    ///
    /// O vencimento sai do fechamento PADRÃO, não do ajustado — é isso que o mantém estável. A
    /// operadora antecipa ou adia o fechamento, mas o dia de vencimento costuma ser fixo; derivá-lo
    /// do fechamento ajustado fazia o vencimento pular um mês inteiro a cada antecipação.
    ///
    /// A regra do dia continua vindo de CardCycleCalculator.DueDateFor: muda de onde ela é chamada,
    /// não a regra. Duplicar calendário já nos custou um bug.
    /// </summary>
    private async Task<(CardCycle Cycle, bool ClosingOverridden, bool DueOverridden)> ApplyOverridesAsync(
        CreditCard card, CardCycle baseCycle, DateOnly cycleReference, CancellationToken ct)
    {
        var previousReference = cycleReference.AddMonths(-1);

        var overrides = await _db.CardCycleOverrides
            .AsNoTracking()
            .Where(o => o.CreditCardId == card.Id
                     && o.GrupoId == _currentUser.GrupoId
                     && (o.ReferenceMonth == cycleReference || o.ReferenceMonth == previousReference))
            .ToDictionaryAsync(o => o.ReferenceMonth, o => o, ct);

        var current = overrides.GetValueOrDefault(cycleReference);

        var closing = current?.ClosingDate ?? baseCycle.ClosingDate;

        var previousBase = CardCycleCalculator.CycleForMonth(card.ClosingDay, card.DueDay, previousReference);
        var previousClosing = overrides.GetValueOrDefault(previousReference)?.ClosingDate
            ?? previousBase.ClosingDate;

        var cycle = new CardCycle(previousClosing.AddDays(1), closing, ResolveDueDate(card, baseCycle, current, closing));

        // As flags vêm do que está GRAVADO, não de comparar datas: o piso também move o
        // vencimento, e compará-lo com o padrão marcaria "ajustado" sem o usuário ter ajustado.
        return (cycle, current?.ClosingDate is not null, current?.DueDate is not null);
    }

    /// <summary>
    /// O vencimento efetivo: o ajustado, se houver; senão o derivado do fechamento PADRÃO.
    ///
    /// E um piso: uma fatura não pode vencer antes de fechar. Quando o fechamento é adiado para
    /// depois do vencimento, este avança para a próxima ocorrência do dia depois do fechamento
    /// efetivo. Isso é correção, não preferência — por isso é piso, e não a volta da derivação.
    /// </summary>
    private static DateOnly ResolveDueDate(
        CreditCard card, CardCycle baseCycle, CardCycleOverride? current, DateOnly effectiveClosing)
    {
        var due = current?.DueDate ?? CardCycleCalculator.DueDateFor(baseCycle.ClosingDate, card.DueDay);

        return due > effectiveClosing
            ? due
            : CardCycleCalculator.DueDateFor(effectiveClosing, card.DueDay);
    }

    /// <summary>
    /// O mês de referência de um ciclo — a sua identidade. Derivado do mês do fechamento, que por
    /// construção (e por invariante, quando ajustado) cai sempre dentro do próprio mês.
    /// </summary>
    internal static DateOnly ReferenceOf(CardCycle cycle) =>
        new(cycle.ClosingDate.Year, cycle.ClosingDate.Month, 1);

    /// <summary>
    /// Resolve o ciclo pedido. A referência é o MÊS (dia 1), não a data de fechamento: o
    /// fechamento pode ser ajustado por fatura, e usá-lo como chave deixaria os pagamentos já
    /// registrados órfãos a cada ajuste.
    /// </summary>
    internal static CardCycle ResolveCycle(int closingDay, int dueDay, DateOnly? reference)
    {
        if (reference is null)
            return CardCycleCalculator.CycleOf(closingDay, dueDay, DateOnly.FromDateTime(DateTime.UtcNow));

        if (reference.Value.Day != 1)
            throw new BusinessRuleException(
                "referencia-invalida",
                $"A referência da fatura é o mês (dia 1), e {reference:dd/MM/yyyy} não é.");

        return CardCycleCalculator.CycleForMonth(closingDay, dueDay, reference.Value);
    }
}
