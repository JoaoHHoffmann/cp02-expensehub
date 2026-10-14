using System;
using System.Linq.Expressions;
using System.Security.Claims;
using ExpenseHub.Api.Domain;
using ExpenseHub.Api.Security;

namespace ExpenseHub.Api.Services;

internal static class ExpenseAccess
{
    public static void EnsureEmployee(ClaimsPrincipal user)
    {
        EnsureRole(user, Roles.Employee);
    }

    public static Expression<Func<Expense, bool>> OwnerFilter(ClaimsPrincipal user)
    {
        EnsureEmployee(user);
        string userId = user.GetUserId();
        return expense => expense.OwnerId == userId;
    }

    public static Expression<Func<Expense, bool>> ReadFilter(ClaimsPrincipal user)
    {
        bool employee = user.IsInRole(Roles.Employee);
        bool approver = user.IsInRole(Roles.Approver);
        bool finance = user.IsInRole(Roles.Finance);
        bool auditor = user.IsInRole(Roles.Auditor);
        if (!employee && !approver && !finance && !auditor)
        {
            throw new DomainException(DomainErrorType.Forbidden, "Sem permissão para consultar reembolsos.");
        }

        string userId = user.GetUserId();
        return expense => auditor
            || (employee && expense.OwnerId == userId)
            || (approver && expense.Status == ExpenseStatus.Submitted)
            || (finance && (expense.Status == ExpenseStatus.Approved || expense.Status == ExpenseStatus.Paid));
    }

    public static void EnsureCanDecide(ClaimsPrincipal user, Expense expense)
    {
        EnsureRole(user, Roles.Approver);
        EnsureNotOwner(user, expense);
        EnsureStatus(expense, ExpenseStatus.Submitted);
    }

    public static void EnsureCanPay(ClaimsPrincipal user, Expense expense)
    {
        EnsureRole(user, Roles.Finance);
        EnsureNotOwner(user, expense);
        EnsureStatus(expense, ExpenseStatus.Approved);
    }

    public static void EnsureApprover(ClaimsPrincipal user)
    {
        EnsureRole(user, Roles.Approver);
    }

    public static void EnsureFinance(ClaimsPrincipal user)
    {
        EnsureRole(user, Roles.Finance);
    }

    private static void EnsureRole(ClaimsPrincipal user, string role)
    {
        if (!user.IsInRole(role))
        {
            throw new DomainException(DomainErrorType.Forbidden, "Sem permissão para esta operação.");
        }
    }

    private static void EnsureNotOwner(ClaimsPrincipal user, Expense expense)
    {
        if (expense.OwnerId == user.GetUserId())
        {
            throw new DomainException(DomainErrorType.Forbidden, "Não é permitido decidir ou pagar o próprio reembolso.");
        }
    }

    private static void EnsureStatus(Expense expense, ExpenseStatus expected)
    {
        if (expense.Status != expected)
        {
            throw new DomainException(DomainErrorType.Conflict, "O estado atual não permite esta operação.");
        }
    }
}
