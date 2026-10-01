using System.Text.Json;
using MeuFinanceiro.Application.Common.Interfaces;
using MeuFinanceiro.Domain.Common;
using MeuFinanceiro.Domain.Entities;
using MeuFinanceiro.Domain.Enums;
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
    public DbSet<AccountMonthlyBalance> AccountMonthlyBalances => Set<AccountMonthlyBalance>();
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
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<CardPayment> CardPayments => Set<CardPayment>();
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

        builder.Entity<AccountMonthlyBalance>(e =>
        {
            // Único: um fechamento por conta/mês. Também é a barreira contra duplicata em corrida
            // entre requisições que gerem o mesmo mês ao mesmo tempo.
            e.HasIndex(b => new { b.AccountId, b.PeriodEnd }).IsUnique();
            e.HasOne(b => b.Account).WithMany().HasForeignKey(b => b.AccountId).OnDelete(DeleteBehavior.Cascade);
        });

        // AuditLog é a tabela que mais cresce (toda SaveChangesAsync grava auditoria) e não tinha
        // índice nenhum: a tela fazia Seq Scan + Sort na tabela inteira. A ordem das colunas segue
        // a consulta real de AuditService.GetAllAsync — filtra por grupo e ordena por Timestamp
        // desc —, e o desc no índice evita o passo de ordenação.
        builder.Entity<AuditLog>(e =>
        {
            e.HasIndex(a => new { a.GrupoId, a.Timestamp }).IsDescending(false, true);
            // Administrador consulta sem filtro de grupo, caindo só na ordenação por data.
            e.HasIndex(a => a.Timestamp).IsDescending();
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

        // Anexo não tem vida própria: sai junto com o lançamento ou o plano que documenta. Cascade
        // aqui (e não o Restrict do resto do modelo) porque não há o que preservar — mas o blob no
        // storage NÃO é apagado pelo banco, então a Application remove os objetos antes de excluir
        // o dono (ver AttachmentService.DeleteForOwnerAsync).
        builder.Entity<Attachment>(e =>
        {
            e.HasIndex(a => a.FinancialTransactionId);
            e.HasIndex(a => a.InstallmentPlanId);
            e.Property(a => a.FileName).HasMaxLength(255);
            e.Property(a => a.ContentType).HasMaxLength(100);
            e.Property(a => a.FileHash).HasMaxLength(64);
            e.Property(a => a.BlobPath).HasMaxLength(512);

            e.HasOne(a => a.FinancialTransaction).WithMany()
                .HasForeignKey(a => a.FinancialTransactionId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(a => a.InstallmentPlan).WithMany()
                .HasForeignKey(a => a.InstallmentPlanId).OnDelete(DeleteBehavior.Cascade);
        });

        // Pagamento de fatura. Restrict em cartao e conta: apagar um cartao ou uma conta que
        // tenha pagamento tem de ser barrado na Application com mensagem clara, como ja e feito
        // para lancamentos, em vez de sumir com o historico financeiro em silencio.
        // SetNull no lancamento porque o pagamento sobrevive a ele: "pago por outra forma" e
        // exatamente um pagamento sem lancamento.
        builder.Entity<CardPayment>(e =>
        {
            e.HasIndex(p => new { p.CreditCardId, p.CycleReference });

            e.HasOne(p => p.CreditCard).WithMany()
                .HasForeignKey(p => p.CreditCardId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(p => p.Account).WithMany()
                .HasForeignKey(p => p.AccountId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(p => p.PaymentTransaction).WithMany()
                .HasForeignKey(p => p.PaymentTransactionId).OnDelete(DeleteBehavior.SetNull);
        });

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
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var auditEntries = BuildAuditEntries();
        if (auditEntries.Count > 0)
            AuditLogs.AddRange(auditEntries);

        // Depois da auditoria (para os deletes derivados não poluírem o log) e antes do commit,
        // na mesma transação.
        await InvalidateAffectedBalancesAsync(cancellationToken);

        return await base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Apaga os fechamentos que deixaram de valer por causa dos lançamentos desta transação.
    ///
    /// Fica aqui, e não nos services, pelo mesmo motivo da auditoria: FinancialTransaction é
    /// criada/alterada/removida em 7 pontos de 5 services diferentes (incluindo a conciliação,
    /// que só muda o Status — e Status muda o saldo). Instrumentar cada um seria frágil.
    ///
    /// Apaga em vez de recalcular: recalcular exigiria reescrever todos os meses até hoje dentro
    /// da transação de escrita, e erros de delta se acumulariam. O DELETE é auto-curativo — o
    /// fechamento é regenerado na próxima leitura, a partir dos lançamentos, que são a verdade.
    ///
    /// PONTO CEGO CONHECIDO: operações em lote do EF (ExecuteUpdate/ExecuteDelete) e scripts SQL
    /// não passam pelo ChangeTracker e, portanto, não disparam esta invalidação. Hoje não existe
    /// nenhuma no projeto, mas o arquivamento de lançamentos antigos (docs/24) é o candidato mais
    /// provável a ser implementado assim — quem o escrever precisa apagar os fechamentos afetados
    /// explicitamente. O endpoint de verificação de saldos existe para detectar essa divergência.
    /// </summary>
    private async Task InvalidateAffectedBalancesAsync(CancellationToken ct)
    {
        var affected = CollectAffectedPeriods();
        if (affected.Count == 0) return;

        foreach (var (accountId, month) in affected)
        {
            var stale = await AccountMonthlyBalances
                .Where(b => b.AccountId == accountId && b.PeriodEnd >= month && !b.IsAnchor)
                .ToListAsync(ct);

            if (stale.Count > 0) AccountMonthlyBalances.RemoveRange(stale);
        }
    }

    /// <summary>
    /// Para cada conta tocada, o MENOR mês afetado. Uma entrada Modified conta pelos dois lados
    /// (original e atual): isso cobre de uma vez mudança de data, troca de conta e mudança de
    /// status, que são justamente os casos em que olhar só o valor novo daria resultado errado.
    /// </summary>
    private Dictionary<Guid, DateOnly> CollectAffectedPeriods()
    {
        var affected = new Dictionary<Guid, DateOnly>();

        void Consider(Guid? accountId, DateOnly date, TransactionStatus status)
        {
            // Pendente não entra no saldo, então não invalida nada.
            if (accountId is not Guid id || status == TransactionStatus.Pending) return;

            var monthEnd = new DateOnly(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month));
            if (!affected.TryGetValue(id, out var current) || monthEnd < current)
                affected[id] = monthEnd;
        }

        foreach (var entry in ChangeTracker.Entries<FinancialTransaction>())
        {
            if (entry.State is EntityState.Added or EntityState.Modified)
                Consider(entry.Entity.AccountId, entry.Entity.TransactionDate, entry.Entity.Status);

            if (entry.State is EntityState.Deleted or EntityState.Modified)
                Consider(
                    entry.OriginalValues.GetValue<Guid?>(nameof(FinancialTransaction.AccountId)),
                    entry.OriginalValues.GetValue<DateOnly>(nameof(FinancialTransaction.TransactionDate)),
                    entry.OriginalValues.GetValue<TransactionStatus>(nameof(FinancialTransaction.Status)));
        }

        return affected;
    }

    // AccountMonthlyBalance é cache derivado: auditar sua invalidação só encheria o log de ruído.
    private static readonly HashSet<Type> AuditExcludedTypes =
        new() { typeof(AuditLog), typeof(RefreshToken), typeof(AccountMonthlyBalance) };

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
