using System;
using System.Collections.Generic;
using System.Security.Claims;
using ExpenseHub.Api.Domain;

namespace ExpenseHub.UnitTests;

internal static class ExpenseTestData
{
    public const string OwnerId = "employee-1";

    public static readonly DateTime NowUtc = new(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);

    public static Expense CreateExpense(ExpenseStatus status = ExpenseStatus.Draft)
    {
        Expense expense = Expense.Create(OwnerId, 1, "Almoço com cliente", 50m, new DateOnly(2026, 10, 1), NowUtc);
        expense.Status = status;
        return expense;
    }

    public static ClaimsPrincipal CreateUser(params string[] roles)
    {
        List<Claim> claims = [new Claim(ClaimTypes.NameIdentifier, OwnerId)];
        foreach (string role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "UnitTest"));
    }
}
