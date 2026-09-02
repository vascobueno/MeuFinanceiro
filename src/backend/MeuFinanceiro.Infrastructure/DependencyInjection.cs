using MeuFinanceiro.Application.Accounts;
using MeuFinanceiro.Application.Admin;
using MeuFinanceiro.Application.Auth;
using MeuFinanceiro.Application.Cards;
using MeuFinanceiro.Application.Categories;
using MeuFinanceiro.Application.Common.Interfaces;
using MeuFinanceiro.Application.Grupos;
using MeuFinanceiro.Application.Imports;
using MeuFinanceiro.Application.Installments;
using MeuFinanceiro.Application.Investments;
using MeuFinanceiro.Application.Invoices;
using MeuFinanceiro.Application.Merchants;
using MeuFinanceiro.Application.Projection;
using MeuFinanceiro.Application.Reconciliation;
using MeuFinanceiro.Application.Recurring;
using MeuFinanceiro.Application.Transactions;
using MeuFinanceiro.Infrastructure.Identity;
using MeuFinanceiro.Infrastructure.Persistence;
using MeuFinanceiro.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MeuFinanceiro.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.Password.RequiredLength = 8;
                options.User.RequireUniqueEmail = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.AllowedForNewUsers = true;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddSignInManager()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        // Token de reset de senha mais curto que o padrão de 1 dia do Identity.
        services.Configure<DataProtectionTokenProviderOptions>(o => o.TokenLifespan = TimeSpan.FromHours(1));

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<IEmailSender, SmtpEmailSender>();
        services.AddHttpClient<ICaptchaVerifier, TurnstileCaptchaVerifier>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IGrupoService, GrupoService>();
        services.AddScoped<IAdminService, AdminService>();

        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<ICardService, CardService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IMerchantService, MerchantService>();
        services.AddScoped<ITransactionService, TransactionService>();
        services.AddScoped<IBankReconciliationService, BankReconciliationService>();
        services.AddScoped<ICardReconciliationService, CardReconciliationService>();
        services.AddScoped<IInvestmentService, InvestmentService>();
        services.AddScoped<IProjectionService, ProjectionService>();
        services.AddScoped<IFileStorageService, AzureBlobStorageService>();
        services.AddScoped<IExcelImportService, ExcelImportService>();
        services.AddScoped<IInvoiceService, InvoiceService>();
        services.AddScoped<IRecurringTransactionService, RecurringTransactionService>();
        services.AddScoped<IInstallmentPlanService, InstallmentPlanService>();

        return services;
    }
}
