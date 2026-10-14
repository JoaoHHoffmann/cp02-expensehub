using System;
using ExpenseHub.Api.Domain;

namespace ExpenseHub.Api.Contracts;

internal sealed record ExpenseHistoryResponse(
    long Id,
    Guid ExpenseId,
    ExpenseAction Action,
    string ActorId,
    DateTime OccurredAtUtc,
    ExpenseStatus? PreviousStatus,
    ExpenseStatus NewStatus,
    string? Justification,
    string? Changes)
{
    public static ExpenseHistoryResponse FromEntity(ExpenseHistory history)
    {
        return new ExpenseHistoryResponse(
            history.Id,
            history.ExpenseId,
            history.Action,
            history.ActorId,
            history.OccurredAtUtc,
            history.PreviousStatus,
            history.NewStatus,
            history.Justification,
            history.Changes);
    }
}
