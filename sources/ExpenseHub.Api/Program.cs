using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ExpenseHub.Api.Endpoints;
using ExpenseHub.Api.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ExpenseHub.Api;

internal static class Program
{
    public static async Task Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
        builder.Services.AddOpenApi();
        builder.Services.AddProblemDetails();
        builder.Services.AddValidation();
        builder.Services.AddDbContext<ExpenseHubDbContext>(options =>
            options.UseSqlite(builder.Configuration.GetConnectionString("ExpenseHub")));

        builder.Services.AddAuthorization();
        builder.Services
            .AddIdentityApiEndpoints<IdentityUser>(options => options.User.RequireUniqueEmail = true)
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<ExpenseHubDbContext>();

        WebApplication app = builder.Build();

        await using (AsyncServiceScope scope = app.Services.CreateAsyncScope())
        {
            ExpenseHubDbContext db = scope.ServiceProvider.GetRequiredService<ExpenseHubDbContext>();
            await db.Database.MigrateAsync();
            await IdentitySeeder.SeedAsync(scope.ServiceProvider, app.Configuration);
        }

        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.UseAuthentication();
        app.UseAuthorization();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
            .WithName("GetHealth");

        app.MapIdentityApi<IdentityUser>();
        app.MapAdminEndpoints();
        app.MapGet("/api/me", GetCurrentUser).RequireAuthorization();

        await app.RunAsync();
    }

    private static IResult GetCurrentUser(ClaimsPrincipal user)
    {
        return Results.Ok(new
        {
            id = user.FindFirstValue(ClaimTypes.NameIdentifier),
            email = user.Identity?.Name,
            roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value),
        });
    }
}
