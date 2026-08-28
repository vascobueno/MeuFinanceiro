using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MeuFinanceiro.Infrastructure.Identity;

/// <summary>
/// Garante a role "Administrator" e promove o e-mail configurado em AdminSeed:Email, se
/// existir um usuário cadastrado com ele. Idempotente — chamado uma vez no startup
/// (Program.cs), sem custo em rodar de novo a cada subida da API.
/// </summary>
public static class IdentitySeeder
{
    public const string AdministratorRole = "Administrator";

    public static async Task SeedAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var configuration = services.GetRequiredService<IConfiguration>();

        if (!await roleManager.RoleExistsAsync(AdministratorRole))
            await roleManager.CreateAsync(new IdentityRole<Guid>(AdministratorRole));

        var adminEmail = configuration["AdminSeed:Email"];
        if (string.IsNullOrWhiteSpace(adminEmail)) return;

        var adminUser = await userManager.FindByEmailAsync(adminEmail);
        if (adminUser is null) return;

        if (!await userManager.IsInRoleAsync(adminUser, AdministratorRole))
            await userManager.AddToRoleAsync(adminUser, AdministratorRole);
    }
}
