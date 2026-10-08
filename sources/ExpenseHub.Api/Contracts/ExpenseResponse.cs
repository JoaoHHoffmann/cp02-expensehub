using System;
using ExpenseHub.Api.Domain;

namespace ExpenseHub.Api.Contracts;

internal sealed record ExpenseResponse(
    Guid Id,
    string OwnerId,
    int CategoryId,
    string Description,
    decimal Amount,
    DateOnly ExpenseDate,
    ExpenseStatus Status,
    DateTime CreatedAtUtc)
{
    public static ExpenseResponse FromEntity(Expense expense)
    {
        return new ExpenseResponse(
            expense.Id,
            expense.OwnerId,
            expense.CategoryId,
            expense.Description,
            expense.Amount,
            expense.ExpenseDate,
            expense.Status,
            expense.CreatedAtUtc);
    }
}
