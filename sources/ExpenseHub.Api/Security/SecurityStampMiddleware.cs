using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

namespace ExpenseHub.Api.Security;

internal sealed class SecurityStampMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityStampMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, SignInManager<IdentityUser> signInManager)
    {
        // Token emitido antes de uma troca de role: o carimbo de segurança não bate mais com o banco.
        if (context.User.Identity?.IsAuthenticated == true
            && await signInManager.ValidateSecurityStampAsync(context.User) is null)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        await _next(context);
    }
}
