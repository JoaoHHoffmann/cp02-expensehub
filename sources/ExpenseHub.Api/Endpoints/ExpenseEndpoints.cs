using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Contracts;
using ExpenseHub.Api.Domain;
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

        group.MapPost("/{id:guid}/submit", SubmitAsync)
            .RequireAuthorization(policy => policy.RequireRole(Roles.Employee));

        group.MapPost("/{id:guid}/approve", ApproveAsync)
            .RequireAuthorization(policy => policy.RequireRole(Roles.Approver));

        group.MapPost("/{id:guid}/reject", RejectAsync)
            .RequireAuthorization(policy => policy.RequireRole(Roles.Approver))
            .AddEndpointFilter<ValidationFilter<RejectExpenseRequest>>();

        group.MapPost("/{id:guid}/pay", PayAsync)
            .RequireAuthorization(policy => policy.RequireRole(Roles.Finance));

        group.MapGet(string.Empty, ListAsync)
            .RequireAuthorization(policy => policy.RequireRole(Roles.Employee, Roles.Approver, Roles.Finance, Roles.Auditor));

        group.MapGet("/{id:guid}", GetAsync)
            .RequireAuthorization(policy => policy.RequireRole(Roles.Employee, Roles.Approver, Roles.Finance, Roles.Auditor));

        group.MapGet("/{id:guid}/history", GetHistoryAsync)
            .RequireAuthorization(policy => policy.RequireRole(Roles.Employee, Roles.Approver, Roles.Finance, Roles.Auditor));
    }

    private static async Task<IResult> ApproveAsync(
        Guid id,
        ClaimsPrincipal user,
        ExpenseService service,
        CancellationToken cancellationToken)
    {
        ExpenseResponse response = await service.ApproveAsync(id, user, cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> RejectAsync(
        Guid id,
        RejectExpenseRequest request,
        ClaimsPrincipal user,
        ExpenseService service,
        CancellationToken cancellationToken)
    {
        ExpenseResponse response = await service.RejectAsync(id, user, request, cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> SubmitAsync(
        Guid id,
        ClaimsPrincipal user,
        ExpenseService service,
        CancellationToken cancellationToken)
    {
        ExpenseResponse response = await service.SubmitAsync(id, user, cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> PayAsync(
        Guid id,
        ClaimsPrincipal user,
        ExpenseService service,
        CancellationToken cancellationToken)
    {
        ExpenseResponse response = await service.PayAsync(id, user, cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> GetHistoryAsync(
        Guid id,
        ClaimsPrincipal user,
        ExpenseService service,
        CancellationToken cancellationToken)
    {
        return Results.Ok(await service.GetHistoryAsync(id, user, cancellationToken));
    }

    private static async Task<IResult> ListAsync(
        ClaimsPrincipal user,
        ExpenseService service,
        CancellationToken cancellationToken)
    {
        return Results.Ok(await service.ListAsync(user, cancellationToken));
    }

    private static async Task<IResult> GetAsync(
        Guid id,
        ClaimsPrincipal user,
        ExpenseService service,
        CancellationToken cancellationToken)
    {
        return Results.Ok(await service.GetAsync(id, user, cancellationToken));
    }

    private static async Task<IResult> CreateAsync(
        ExpenseRequest request,
        ClaimsPrincipal user,
        ExpenseService service,
        CancellationToken cancellationToken)
    {
        ExpenseResponse response = await service.CreateAsync(user, request, cancellationToken);
        return Results.Created($"/api/expenses/{response.Id}", response);
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        ExpenseRequest request,
        ClaimsPrincipal user,
        ExpenseService service,
        CancellationToken cancellationToken)
    {
        ExpenseResponse response = await service.UpdateAsync(id, user, request, cancellationToken);
        return Results.Ok(response);
    }
}
