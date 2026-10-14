using System;
using System.Linq.Expressions;
using System.Security.Claims;
using ExpenseHub.Api.Domain;
using ExpenseHub.Api.Security;

namespace ExpenseHub.Api.Services;

internal static class ExpenseAccess
{
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
}
