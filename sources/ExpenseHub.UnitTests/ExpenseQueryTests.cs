using System.Linq;
using ExpenseHub.Api.Domain;
using ExpenseHub.Api.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests;

/// <summary>Verifica o escopo de leitura usado nas consultas do EF Core.</summary>
[TestClass]
public sealed class ExpenseQueryTests
{
    /// <summary>Employee consulta todos os estados próprios e nenhum recurso alheio.</summary>
    [TestMethod]
    public void EmployeeSeesOwnExpensesInEveryStateButNeverAnotherEmployeesExpenses()
    {
        foreach (ExpenseStatus status in System.Enum.GetValues<ExpenseStatus>())
        {
            Expense own = ExpenseTestData.CreateExpense(status);
            Expense other = ExpenseTestData.CreateExpense(status);
            other.OwnerId = "employee-2";
            Expense[] expenses = [own, other];

            Expense[] visible = expenses.AsQueryable()
                .Where(ExpenseAccess.ReadFilter(ExpenseTestData.CreateUser(Roles.Employee)))
                .ToArray();

            CollectionAssert.AreEqual(new[] { own }, visible);
        }
    }

    /// <summary>Os revisores consultam somente os estados concedidos pela role.</summary>
    /// <param name="role">Role do usuário.</param>
    /// <param name="visibleStatus">Estado que deve estar visível.</param>
    [TestMethod]
    [DataRow(Roles.Approver, (int)ExpenseStatus.Submitted)]
    [DataRow(Roles.Finance, (int)ExpenseStatus.Approved)]
    [DataRow(Roles.Finance, (int)ExpenseStatus.Paid)]
    public void ReviewerSeesOnlyStatesGrantedByTheirRole(string role, int visibleStatus)
    {
        Expense[] expenses = System.Enum.GetValues<ExpenseStatus>()
            .Select(ExpenseTestData.CreateExpense).ToArray();

        Expense[] visible = expenses.AsQueryable()
            .Where(ExpenseAccess.ReadFilter(ExpenseTestData.CreateUser(role)))
            .ToArray();

        Assert.IsTrue(visible.Any(e => e.Status == (ExpenseStatus)visibleStatus));
        Assert.IsTrue(visible.All(e => role == Roles.Approver
            ? e.Status == ExpenseStatus.Submitted
            : e.Status is ExpenseStatus.Approved or ExpenseStatus.Paid));
    }

    /// <summary>Auditor consulta todos os estados e proprietários.</summary>
    [TestMethod]
    public void AuditorSeesEveryStateAndOwner()
    {
        Expense[] expenses = System.Enum.GetValues<ExpenseStatus>()
            .Select(ExpenseTestData.CreateExpense).ToArray();
        expenses[0].OwnerId = "employee-2";

        Expense[] visible = expenses.AsQueryable()
            .Where(ExpenseAccess.ReadFilter(ExpenseTestData.CreateUser(Roles.Auditor)))
            .ToArray();

        CollectionAssert.AreEqual(expenses, visible);
    }

    /// <summary>Roles sem acesso funcional são recusadas.</summary>
    /// <param name="role">Role que não concede leitura.</param>
    [TestMethod]
    [DataRow(Roles.Admin)]
    [DataRow("Unknown")]
    public void RoleWithoutFunctionalAccessIsForbidden(string role)
    {
        DomainException exception = Assert.ThrowsExactly<DomainException>(() =>
            ExpenseAccess.ReadFilter(ExpenseTestData.CreateUser(role)));

        Assert.AreEqual(DomainErrorType.Forbidden, exception.Type);
    }

    /// <summary>Uma identidade autenticada sem role não consulta reembolsos.</summary>
    [TestMethod]
    public void UserWithoutRolesIsForbidden()
    {
        DomainException exception = Assert.ThrowsExactly<DomainException>(() =>
            ExpenseAccess.ReadFilter(ExpenseTestData.CreateUser()));

        Assert.AreEqual(DomainErrorType.Forbidden, exception.Type);
    }
}
