using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Contracts;
using ExpenseHub.Api.Domain;
using ExpenseHub.Api.Security;
using ExpenseHub.Api.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ExpenseHub.Api.Endpoints;

internal static class ExpenseEndpoints
{
    public static void MapExpenseEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/expenses").RequireAuthorization();

        group.MapPost(string.Empty, CreateAsync)
            .RequireAuthorization(policy => policy.RequireRole(Roles.Employee))
            .AddEndpointFilter<ValidationFilter<ExpenseRequest>>();

        group.MapPut("/{id:guid}", UpdateAsync)
            .RequireAuthorization(policy => policy.RequireRole(Roles.Employee))
            .AddEndpointFilter<ValidationFilter<ExpenseRequest>>();
    }

    private static async Task<IResult> CreateAsync(
        ExpenseRequest request,
        ClaimsPrincipal user,
        ExpenseService service,
        CancellationToken cancellationToken)
    {
        ExpenseResponse response = await service.CreateAsync(user.GetUserId(), request, cancellationToken);
        return Results.Created($"/api/expenses/{response.Id}", response);
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        ExpenseRequest request,
        ClaimsPrincipal user,
        ExpenseService service,
        CancellationToken cancellationToken)
    {
        ExpenseResponse response = await service.UpdateAsync(id, user.GetUserId(), request, cancellationToken);
        return Results.Ok(response);
    }
}
