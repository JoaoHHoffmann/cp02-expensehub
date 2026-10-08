using System;
using System.Linq;
using System.Threading.Tasks;
using ExpenseHub.Api.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ExpenseHub.Api.Data;

internal static class IdentitySeeder
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration)
    {
        RoleManager<IdentityRole> roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (string role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                EnsureSucceeded(await roleManager.CreateAsync(new IdentityRole(role)));
            }
        }

        string adminEmail = configuration["Seed:AdminEmail"]
            ?? throw new InvalidOperationException("Configure Seed:AdminEmail no appsettings.json.");
        string adminPassword = configuration["Seed:AdminPassword"]
            ?? throw new InvalidOperationException("Configure Seed:AdminPassword com user-secrets ou variável de ambiente.");

        UserManager<IdentityUser> userManager = services.GetRequiredService<UserManager<IdentityUser>>();
        IdentityUser? admin = await userManager.FindByEmailAsync(adminEmail);
        if (admin is null)
        {
            admin = new IdentityUser { UserName = adminEmail, Email = adminEmail, EmailConfirmed = true };
            EnsureSucceeded(await userManager.CreateAsync(admin, adminPassword));
        }

        if (!await userManager.IsInRoleAsync(admin, Roles.Admin))
        {
            EnsureSucceeded(await userManager.AddToRoleAsync(admin, Roles.Admin));
        }
    }

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            string errors = string.Join("; ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Falha no seed do Identity: {errors}");
        }
    }
}
