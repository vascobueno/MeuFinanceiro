using System.Text.Json;
using MeuFinanceiro.Application.Common.Interfaces;
using MeuFinanceiro.Domain.Common;
using MeuFinanceiro.Domain.Entities;
using MeuFinanceiro.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace MeuFinanceiro.Infrastructure.Persistence;

/// <summary>
/// Contexto EF Core. ORM/migrations versionadas e valores decimal(18,2) conforme
/// docs/15-database.md. Implementa IApplicationDbContext para a Application depender
/// apenas da abstração.
/// </summary>
public class AppDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>, IApplicationDbContext
{
    private readonly ICurrentUserService _currentUser;

    public AppDbContext(DbContextOptions<AppDbContext> options, ICurrentUserService currentUser) : base(options)
    {
        _currentUser = currentUser;
    }

    public DbSet<Grupo> Grupos => Set<Grupo>();
    public DbSet<GrupoMember> GrupoMembers => Set<GrupoMember>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<CreditCard> CreditCards => Set<CreditCard>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Subcategory> Subcategories => Set<Subcategory>();
    public DbSet<Merchant> Merchants => Set<Merchant>();
    public DbSet<MerchantAlias> MerchantAliases => Set<MerchantAlias>();
    public DbSet<FinancialTransaction> FinancialTransactions => Set<FinancialTransaction>();
    public DbSet<RecurringTransaction> RecurringTransactions => Set<RecurringTransaction>();
    public DbSet<RecurringTransactionVersion> RecurringTransactionVersions => Set<RecurringTransactionVersion>();
    public DbSet<InstallmentPlan> InstallmentPlans => Set<InstallmentPlan>();
    public DbSet<Installment> Installments => Set<Installment>();
    public DbSet<BankStatement> BankStatements => Set<BankStatement>();
    public DbSet<BankStatementItem> BankStatementItems => Set<BankStatementItem>();
    public DbSet<CardInvoice> CardInvoices => Set<CardInvoice>();
    public DbSet<CardInvoiceItem> CardInvoiceItems => Set<CardInvoiceItem>();
    public DbSet<Reconciliation> Reconciliations => Set<Reconciliation>();
    public DbSet<InvestmentAccount> InvestmentAccounts => Set<InvestmentAccount>();
    public DbSet<Investment> Investments => Set<Investment>();
    public DbSet<InvestmentPosition> InvestmentPositions => Set<InvestmentPosition>();
    public DbSet<InvestmentTransaction> InvestmentTransactions => Set<InvestmentTransaction>();
    public DbSet<ImportFile> ImportFiles => Set<ImportFile>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Valores financeiros: decimal(18,2) em toda a base, nunca float/double (regra 20).
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(decimal) || property.ClrType == typeof(decimal?))
                    property.SetColumnType("decimal(18,2)");
            }
        }

        // FK de cada entidade "de grupo" para Grupos(Id) — sem navigation property
        // em nenhum dos dois lados (mesmo padrão do resto do Domain), só a constraint no banco.
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            if (typeof(GrupoOwnedEntity).IsAssignableFrom(entityType.ClrType))
            {
                builder.Entity(entityType.ClrType)
                    .HasOne(typeof(Grupo))
                    .WithMany()
                    .HasForeignKey(nameof(GrupoOwnedEntity.GrupoId))
                    .OnDelete(DeleteBehavior.Restrict);
            }
        }

        // Installment.FinancialTransactionId é apenas um campo de referência (preenchido pela
        // Application ao criar o lançamento da parcela); o relacionamento navegável "oficial"
        // é o inverso (FinancialTransaction.InstallmentId abaixo), para evitar duas relações
        // independentes e ambíguas entre as mesmas duas entidades. Precisa vir antes da
        // configuração de FinancialTransaction abaixo: ignorar a navegação depois de ela já
        // ter sido referenciada num .WithOne(...) faz o EF Core recriar a relação como uma
        // segunda FK sombra (ex.: "InstallmentId1").
        builder.Entity<Installment>().Ignore(i => i.FinancialTransaction);

        // Índices baseados nas consultas de docs/04-modelo-dados.md e docs/06-lancamentos.md.
        builder.Entity<FinancialTransaction>(e =>
        {
            e.HasIndex(t => new { t.GrupoId, t.TransactionDate });
            e.HasIndex(t => new { t.GrupoId, t.Description });
            e.HasIndex(t => t.MerchantId);
            e.HasIndex(t => t.Status);

            // Evita múltiplos caminhos de cascade delete no banco: exclusão de
            // conta/cartão/categoria/etc. é bloqueada explicitamente na Application
            // quando há lançamentos vinculados (ver *Service.DeleteAsync).
            e.HasOne(t => t.Account).WithMany(a => a.Transactions).HasForeignKey(t => t.AccountId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(t => t.CreditCard).WithMany(c => c.Transactions).HasForeignKey(t => t.CreditCardId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(t => t.Category).WithMany().HasForeignKey(t => t.CategoryId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(t => t.Subcategory).WithMany().HasForeignKey(t => t.SubcategoryId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(t => t.Merchant).WithMany().HasForeignKey(t => t.MerchantId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(t => t.Installment).WithOne().HasForeignKey<FinancialTransaction>(t => t.InstallmentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(t => t.RecurringTransaction).WithMany().HasForeignKey(t => t.RecurringTransactionId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<BankStatementItem>(e => e.HasIndex(i => i.Date));
        builder.Entity<CardInvoiceItem>(e => e.HasIndex(i => i.Date));
        builder.Entity<Reconciliation>(e => e.HasIndex(r => r.Status));
        builder.Entity<Merchant>(e => e.HasIndex(m => new { m.GrupoId, m.Name }));

        builder.Entity<GrupoMember>(e =>
        {
            e.HasIndex(m => m.UserId).IsUnique();
            e.HasOne<Grupo>().WithMany().HasForeignKey(m => m.GrupoId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<InstallmentPlan>()
            .HasMany(p => p.Installments)
            .WithOne(i => i.InstallmentPlan)
            .HasForeignKey(i => i.InstallmentPlanId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<RecurringTransaction>()
            .HasMany(r => r.Versions)
            .WithOne(v => v.RecurringTransaction)
            .HasForeignKey(v => v.RecurringTransactionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Category>()
            .HasMany(c => c.Subcategories)
            .WithOne(s => s.Category)
            .HasForeignKey(s => s.CategoryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<RefreshToken>(e =>
        {
            e.HasKey(r => r.Id);
            e.HasIndex(r => r.TokenHash);
            e.HasOne(r => r.User).WithMany(u => u.RefreshTokens).HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ApplicationUser>().Property(u => u.Name).HasMaxLength(200);
    }

    /// <summary>
    /// Auditoria automática de Create/Update/Delete (docs/17-auditoria.md, Parte 5 do plano de
    /// Grupo): antes só 2 de 13 serviços chamavam IAuditService manualmente — isso cobre as
    /// 12 entidades de grupo (e qualquer entidade nova) de uma vez, sem precisar lembrar de
    /// instrumentar cada serviço. CardReconciliationService/BankReconciliationService continuam
    /// com suas chamadas manuais específicas (ações como "Accept"/"Undo" têm semântica própria
    /// que este override genérico não capturaria) — as duas formas convivem sem conflito.
    /// </summary>
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var auditEntries = BuildAuditEntries();
        if (auditEntries.Count > 0)
            AuditLogs.AddRange(auditEntries);

        return base.SaveChangesAsync(cancellationToken);
    }

    private static readonly HashSet<Type> AuditExcludedTypes = new() { typeof(AuditLog), typeof(RefreshToken) };

    private List<AuditLog> BuildAuditEntries()
    {
        var entries = new List<AuditLog>();

        foreach (var entry in ChangeTracker.Entries())
        {
            if (AuditExcludedTypes.Contains(entry.Entity.GetType())) continue;
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted)) continue;

            var grupoId = entry.Entity is GrupoOwnedEntity owned ? owned.GrupoId : (Guid?)null;
            var pkValue = entry.Properties.FirstOrDefault(p => p.Metadata.IsPrimaryKey())?.CurrentValue?.ToString() ?? string.Empty;

            string? oldValues = entry.State is EntityState.Modified or EntityState.Deleted
                ? JsonSerializer.Serialize(entry.Properties.ToDictionary(p => p.Metadata.Name, p => p.OriginalValue))
                : null;

            string? newValues = entry.State is EntityState.Modified or EntityState.Added
                ? JsonSerializer.Serialize(entry.Properties.ToDictionary(p => p.Metadata.Name, p => p.CurrentValue))
                : null;

            entries.Add(new AuditLog
            {
                UserId = _currentUser.IsAuthenticated ? _currentUser.UserId : null,
                GrupoId = grupoId,
                Action = entry.State.ToString(),
                EntityName = entry.Entity.GetType().Name,
                EntityId = pkValue,
                OldValues = oldValues,
                NewValues = newValues,
                CorrelationId = Guid.NewGuid().ToString()
            });
        }

        return entries;
    }
}
