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
    DateTime CreatedAtUtc,
    PaymentResponse? Payment)
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
            expense.CreatedAtUtc,
            expense.Payment is null ? null : PaymentResponse.FromEntity(expense.Payment));
    }
}

internal sealed record PaymentResponse(Guid Id, string PaidById, DateTime PaidAtUtc, decimal Amount)
{
    public static PaymentResponse FromEntity(PaymentRecord payment)
    {
        return new PaymentResponse(payment.Id, payment.PaidById, payment.PaidAtUtc, payment.Amount);
    }
}
