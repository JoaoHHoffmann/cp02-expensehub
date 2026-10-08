using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;

namespace ExpenseHub.Api.Security;

internal sealed class SecurityStampClaimsTransformation : IClaimsTransformation
{
    private readonly SignInManager<IdentityUser> _signInManager;

    public SecurityStampClaimsTransformation(SignInManager<IdentityUser> signInManager)
    {
        _signInManager = signInManager;
    }

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true)
        {
            return principal;
        }

        IdentityUser? user = await _signInManager.ValidateSecurityStampAsync(principal);

        // Carimbo diferente do banco: token emitido antes de uma troca de role.
        // Devolve um usuário anônimo, e a rota protegida responde 401.
        return user is null ? new ClaimsPrincipal(new ClaimsIdentity()) : principal;
    }
}
