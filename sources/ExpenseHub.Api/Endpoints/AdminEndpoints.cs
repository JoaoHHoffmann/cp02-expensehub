using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Contracts;
using ExpenseHub.Api.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace ExpenseHub.Api.Endpoints;

internal static class AdminEndpoints
{
    public static void MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/admin/users")
            .RequireAuthorization(policy => policy.RequireRole(Roles.Admin));

        group.MapGet(string.Empty, ListUsersAsync);
        group.MapPut("/{id}/roles", UpdateRolesAsync)
            .AddEndpointFilter<ValidationFilter<UpdateUserRolesRequest>>();
    }

    private static async Task<IResult> ListUsersAsync(
        UserManager<IdentityUser> userManager,
        CancellationToken cancellationToken)
    {
        List<IdentityUser> users = await userManager.Users
            .OrderBy(u => u.Email)
            .ToListAsync(cancellationToken);

        List<UserResponse> response = [];
        foreach (IdentityUser user in users)
        {
            IList<string> roles = await userManager.GetRolesAsync(user);
            response.Add(new UserResponse(user.Id, user.Email, roles));
        }

        return Results.Ok(response);
    }

    private static async Task<IResult> UpdateRolesAsync(
        string id,
        UpdateUserRolesRequest request,
        ClaimsPrincipal currentUser,
        UserManager<IdentityUser> userManager)
    {
        List<string> requested = request.Roles.Distinct(StringComparer.Ordinal).ToList();
        List<string> unknown = requested.Where(r => !Roles.All.Contains(r)).ToList();
        if (unknown.Count > 0)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["roles"] = [$"Roles desconhecidas: {string.Join(", ", unknown)}."],
            });
        }

        IdentityUser? user = await userManager.FindByIdAsync(id);
        if (user is null)
        {
            return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Usuário não encontrado.");
        }

        bool isSelf = user.Id == userManager.GetUserId(currentUser);
        if (isSelf && !requested.Contains(Roles.Admin))
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "O Admin não pode remover a própria role Admin.");
        }

        IList<string> current = await userManager.GetRolesAsync(user);
        IdentityResult removed = await userManager.RemoveFromRolesAsync(user, current.Except(requested));
        IdentityResult added = await userManager.AddToRolesAsync(user, requested.Except(current));
        if (!removed.Succeeded || !added.Succeeded)
        {
            return Results.Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Falha ao atualizar as roles.");
        }

        // Invalida os tokens emitidos antes da alteração: o usuário precisa fazer login de novo.
        await userManager.UpdateSecurityStampAsync(user);

        return Results.Ok(new UserResponse(user.Id, user.Email, requested));
    }
}
