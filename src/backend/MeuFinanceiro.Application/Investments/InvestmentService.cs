using MeuFinanceiro.Application.Common.Exceptions;
using MeuFinanceiro.Application.Common.Interfaces;
using MeuFinanceiro.Domain.Entities;
using MeuFinanceiro.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MeuFinanceiro.Application.Investments;

/// <summary>
/// docs/10-investimentos.md + docs/05-regras-negocio.md: saldo separado das contas correntes
/// (regra 6/7); aplicação reduz conta e aumenta investimento; resgate o inverso; rendimento é
/// evento separado (não mexe na conta); nunca inventar rentabilidade futura.
/// </summary>
public class InvestmentService : IInvestmentService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public InvestmentService(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<InvestmentsSummaryResponse> GetSummaryAsync(CancellationToken ct = default)
    {
        var accounts = await _db.InvestmentAccounts
            .AsNoTracking()
            .Include(a => a.Investments).ThenInclude(i => i.Positions)
            .Where(a => a.GrupoId == _currentUser.GrupoId)
            .ToListAsync(ct);

        var responses = accounts.Select(ToAccountResponse).ToList();
        return new InvestmentsSummaryResponse(responses.Sum(a => a.TotalPosition), responses);
    }

    public async Task<InvestmentAccountResponse> CreateAccountAsync(CreateInvestmentAccountRequest request, CancellationToken ct = default)
    {
        var account = new InvestmentAccount
        {
            GrupoId = _currentUser.GrupoId,
            Institution = request.Institution,
            Name = request.Name,
            CreatedBy = _currentUser.UserId
        };

        _db.InvestmentAccounts.Add(account);
        await _db.SaveChangesAsync(ct);
        return new InvestmentAccountResponse(account.Id, account.Institution, account.Name, 0);
    }

    public async Task<IReadOnlyList<InvestmentResponse>> GetInvestmentsAsync(Guid? investmentAccountId, CancellationToken ct = default)
    {
        var query = _db.Investments
            .AsNoTracking()
            .Include(i => i.Positions)
            .Include(i => i.InvestmentAccount)
            .Where(i => i.InvestmentAccount!.GrupoId == _currentUser.GrupoId);

        if (investmentAccountId is not null)
            query = query.Where(i => i.InvestmentAccountId == investmentAccountId);

        var investments = await query.ToListAsync(ct);
        return investments.Select(ToInvestmentResponse).ToList();
    }

    public async Task<InvestmentResponse> CreateInvestmentAsync(CreateInvestmentRequest request, CancellationToken ct = default)
    {
        var account = await _db.InvestmentAccounts.FirstOrDefaultAsync(a => a.Id == request.InvestmentAccountId, ct)
            ?? throw new NotFoundException(nameof(InvestmentAccount), request.InvestmentAccountId);
        if (account.GrupoId != _currentUser.GrupoId) throw new ForbiddenException();

        var investment = new Investment
        {
            InvestmentAccountId = request.InvestmentAccountId,
            Name = request.Name,
            Type = request.Type,
            MaturityDate = request.MaturityDate,
            Liquidity = request.Liquidity
        };

        _db.Investments.Add(investment);
        await _db.SaveChangesAsync(ct);
        return new InvestmentResponse(investment.Id, investment.InvestmentAccountId, investment.Name, investment.Type, investment.MaturityDate, investment.Liquidity, 0, 0);
    }

    public async Task<InvestmentTransactionResponse> RegisterTransactionAsync(CreateInvestmentTransactionRequest request, CancellationToken ct = default)
    {
        var investment = await _db.Investments
            .Include(i => i.InvestmentAccount)
            .Include(i => i.Positions)
            .FirstOrDefaultAsync(i => i.Id == request.InvestmentId, ct)
            ?? throw new NotFoundException(nameof(Investment), request.InvestmentId);
        if (investment.InvestmentAccount!.GrupoId != _currentUser.GrupoId) throw new ForbiddenException();

        if (request.Amount <= 0)
            throw new BusinessRuleException("valor-invalido", "O valor da movimentação de investimento deve ser maior que zero.");

        Guid? financialTransactionId = null;

        if (request.Type is InvestmentTransactionType.Application or InvestmentTransactionType.Redemption)
        {
            if (request.AccountId is null)
                throw new BusinessRuleException("conta-obrigatoria", "Aplicações e resgates precisam informar a conta corrente de origem/destino.");

            var account = await _db.Accounts.FirstOrDefaultAsync(a => a.Id == request.AccountId, ct)
                ?? throw new NotFoundException(nameof(Account), request.AccountId);
            if (account.GrupoId != _currentUser.GrupoId) throw new ForbiddenException();

            // Regra 10: aplicação reduz conta / resgate aumenta conta. Nenhum dos dois é
            // receita/despesa operacional — refletido no Type dedicado (regra 10/11).
            var financialTransaction = new FinancialTransaction
            {
                GrupoId = _currentUser.GrupoId,
                TransactionDate = request.Date,
                Description = $"{(request.Type == InvestmentTransactionType.Application ? "Aplicação" : "Resgate")} — {investment.Name}",
                AccountId = account.Id,
                Type = request.Type == InvestmentTransactionType.Application ? TransactionType.InvestmentApplication : TransactionType.InvestmentRedemption,
                Direction = request.Type == InvestmentTransactionType.Application ? TransactionDirection.Debit : TransactionDirection.Credit,
                Amount = request.Amount,
                Status = TransactionStatus.Confirmed,
                CreatedBy = _currentUser.UserId
            };

            _db.FinancialTransactions.Add(financialTransaction);
            financialTransactionId = financialTransaction.Id;
        }

        var investmentTransaction = new InvestmentTransaction
        {
            InvestmentId = investment.Id,
            Type = request.Type,
            Date = request.Date,
            Amount = request.Amount,
            FinancialTransactionId = financialTransactionId
        };
        _db.InvestmentTransactions.Add(investmentTransaction);

        var currentPosition = investment.Positions.OrderByDescending(p => p.Date).ThenByDescending(p => p.CreatedAt).FirstOrDefault();
        var currentPrincipal = currentPosition?.Principal ?? 0m;
        var currentNetValue = currentPosition?.NetValue ?? 0m;

        var (newPrincipal, newNetValue) = request.Type switch
        {
            InvestmentTransactionType.Application => (currentPrincipal + request.Amount, currentNetValue + request.Amount),
            InvestmentTransactionType.Redemption => (currentPrincipal - request.Amount, currentNetValue - request.Amount),
            InvestmentTransactionType.Yield => (currentPrincipal, currentNetValue + request.Amount), // rendimento não altera o principal aportado
            _ => (currentPrincipal, currentNetValue)
        };

        _db.InvestmentPositions.Add(new InvestmentPosition
        {
            InvestmentId = investment.Id,
            Date = request.Date,
            Principal = newPrincipal,
            GrossValue = newNetValue,
            TaxWithheld = 0,
            NetValue = newNetValue
        });

        await _db.SaveChangesAsync(ct);

        return new InvestmentTransactionResponse(investmentTransaction.Id, investment.Id, request.Type, request.Date, request.Amount, financialTransactionId);
    }

    // ----------------- helpers -----------------

    private static InvestmentAccountResponse ToAccountResponse(InvestmentAccount account)
    {
        var total = account.Investments
            .Select(i => i.Positions.OrderByDescending(p => p.Date).ThenByDescending(p => p.CreatedAt).FirstOrDefault())
            .Where(p => p is not null)
            .Sum(p => p!.NetValue);

        return new InvestmentAccountResponse(account.Id, account.Institution, account.Name, total);
    }

    private static InvestmentResponse ToInvestmentResponse(Investment investment)
    {
        var position = investment.Positions.OrderByDescending(p => p.Date).ThenByDescending(p => p.CreatedAt).FirstOrDefault();
        return new InvestmentResponse(investment.Id, investment.InvestmentAccountId, investment.Name, investment.Type,
            investment.MaturityDate, investment.Liquidity, position?.Principal ?? 0m, position?.NetValue ?? 0m);
    }
}
