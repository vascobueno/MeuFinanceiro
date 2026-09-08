using MeuFinanceiro.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MeuFinanceiro.Application.Common.Interfaces;

/// <summary>
/// Abstração do contexto de dados exposta à camada Application (implementada pelo
/// AppDbContext em Infrastructure) — mantém o Domain/Application livres de EF Core direto
/// nos serviços, mas sem a sobrecarga de um repositório por entidade.
/// </summary>
public interface IApplicationDbContext
{
    DbSet<Grupo> Grupos { get; }
    DbSet<GrupoMember> GrupoMembers { get; }
    DbSet<Account> Accounts { get; }
    DbSet<CreditCard> CreditCards { get; }
    DbSet<Category> Categories { get; }
    DbSet<Subcategory> Subcategories { get; }
    DbSet<Merchant> Merchants { get; }
    DbSet<MerchantAlias> MerchantAliases { get; }
    DbSet<FinancialTransaction> FinancialTransactions { get; }
    DbSet<RecurringTransaction> RecurringTransactions { get; }
    DbSet<RecurringTransactionVersion> RecurringTransactionVersions { get; }
    DbSet<InstallmentPlan> InstallmentPlans { get; }
    DbSet<Installment> Installments { get; }
    DbSet<BankStatement> BankStatements { get; }
    DbSet<BankStatementItem> BankStatementItems { get; }
    DbSet<CardInvoice> CardInvoices { get; }
    DbSet<CardInvoiceItem> CardInvoiceItems { get; }
    DbSet<MeuFinanceiro.Domain.Entities.Reconciliation> Reconciliations { get; }
    DbSet<InvestmentAccount> InvestmentAccounts { get; }
    DbSet<Investment> Investments { get; }
    DbSet<InvestmentPosition> InvestmentPositions { get; }
    DbSet<InvestmentTransaction> InvestmentTransactions { get; }
    DbSet<ImportFile> ImportFiles { get; }
    DbSet<AuditLog> AuditLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
