using System.Collections.Generic;

namespace ExpenseHub.Api.Domain;

internal static class Roles
{
    public const string Admin = "Admin";
    public const string Employee = "Employee";
    public const string Approver = "Approver";
    public const string Finance = "Finance";
    public const string Auditor = "Auditor";

    public static readonly IReadOnlyList<string> All = [Admin, Employee, Approver, Finance, Auditor];
}
