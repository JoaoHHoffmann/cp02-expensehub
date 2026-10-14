using System;
using System.Linq;
using System.Security.Claims;
using ExpenseHub.Api.Domain;
using ExpenseHub.Api.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests;

/// <summary>Verifica a combinação de roles, ownership e estado sem banco de dados.</summary>
[TestClass]
public sealed class ExpenseAccessTests
{
    /// <summary>O filtro de escrita admite somente o próprio recurso de um Employee.</summary>
    /// <param name="status">Estado do recurso usado no filtro.</param>
    [TestMethod]
    [DataRow((int)ExpenseStatus.Draft)]
    [DataRow((int)ExpenseStatus.Submitted)]
    [DataRow((int)ExpenseStatus.Approved)]
    [DataRow((int)ExpenseStatus.Rejected)]
    [DataRow((int)ExpenseStatus.Paid)]
    public void OwnerFilter_Employee_IsRestrictedToOwnExpenses(int status)
    {
        Func<Expense, bool> filter = ExpenseAccess.OwnerFilter(ExpenseTestData.CreateUser(Roles.Employee)).Compile();
        Expense own = ExpenseTestData.CreateExpense((ExpenseStatus)status);
        Expense other = CreateOtherExpense((ExpenseStatus)status);

        Assert.IsTrue(filter(own));
        Assert.IsFalse(filter(other));
    }

    /// <summary>Roles de consulta ou administração não concedem escrita de Employee.</summary>
    /// <param name="role">Role sem permissão de escrita, ou null para nenhuma role.</param>
    [TestMethod]
    [DataRow(Roles.Admin)]
    [DataRow(Roles.Auditor)]
    [DataRow(Roles.Finance)]
    [DataRow(Roles.Approver)]
    [DataRow(null)]
    public void OwnerFilter_WithoutEmployee_IsForbidden(string? role)
    {
        ClaimsPrincipal user = CreateUserWithOptionalRole(role);

        DomainException error = Assert.ThrowsExactly<DomainException>(() => ExpenseAccess.OwnerFilter(user));

        Assert.AreEqual(DomainErrorType.Forbidden, error.Type);
    }

    /// <summary>Adicionar Auditor a Employee não permite editar ou enviar recursos alheios.</summary>
    [TestMethod]
    public void OwnerFilter_MultipleRoles_DoesNotExpandOwnership()
    {
        ClaimsPrincipal user = ExpenseTestData.CreateUser(Roles.Employee, Roles.Auditor, Roles.Approver, Roles.Finance, Roles.Admin);
        Func<Expense, bool> filter = ExpenseAccess.OwnerFilter(user).Compile();

        Assert.IsTrue(filter(ExpenseTestData.CreateExpense()));
        Assert.IsFalse(filter(CreateOtherExpense(ExpenseStatus.Draft)));
        Assert.IsFalse(filter(CreateOtherExpense(ExpenseStatus.Submitted)));
    }

    /// <summary>Roles adicionais conservam a permissão concedida por Employee.</summary>
    /// <param name="additionalRole">Role acumulada com Employee.</param>
    [TestMethod]
    [DataRow(Roles.Admin)]
    [DataRow(Roles.Auditor)]
    [DataRow(Roles.Approver)]
    [DataRow(Roles.Finance)]
    public void EnsureEmployee_WithAdditionalRole_IsAllowed(string additionalRole)
    {
        ClaimsPrincipal user = ExpenseTestData.CreateUser(Roles.Employee, additionalRole);

        ExpenseAccess.EnsureEmployee(user);
    }

    /// <summary>A combinação Employee, Approver e Finance acumula os escopos de leitura.</summary>
    /// <param name="status">Estado do recurso.</param>
    /// <param name="otherIsVisible">Visibilidade esperada para um recurso alheio.</param>
    [TestMethod]
    [DataRow((int)ExpenseStatus.Draft, 0)]
    [DataRow((int)ExpenseStatus.Submitted, 1)]
    [DataRow((int)ExpenseStatus.Approved, 1)]
    [DataRow((int)ExpenseStatus.Rejected, 0)]
    [DataRow((int)ExpenseStatus.Paid, 1)]
    public void ReadFilter_MultipleFunctionalRoles_CombinesVisibility(int status, int otherIsVisible)
    {
        ClaimsPrincipal user = ExpenseTestData.CreateUser(Roles.Employee, Roles.Approver, Roles.Finance);
        Func<Expense, bool> filter = ExpenseAccess.ReadFilter(user).Compile();

        Assert.IsTrue(filter(ExpenseTestData.CreateExpense((ExpenseStatus)status)));
        Assert.AreEqual(otherIsVisible == 1, filter(CreateOtherExpense((ExpenseStatus)status)));
    }

    /// <summary>Admin conserva o escopo funcional das roles acumuladas sem ampliá-lo.</summary>
    /// <param name="status">Estado do recurso.</param>
    [TestMethod]
    [DataRow((int)ExpenseStatus.Draft)]
    [DataRow((int)ExpenseStatus.Submitted)]
    [DataRow((int)ExpenseStatus.Approved)]
    [DataRow((int)ExpenseStatus.Rejected)]
    [DataRow((int)ExpenseStatus.Paid)]
    public void ReadFilter_AdminWithEmployeeOrAuditor_UsesFunctionalRole(int status)
    {
        Func<Expense, bool> employeeFilter = ExpenseAccess.ReadFilter(ExpenseTestData.CreateUser(Roles.Admin, Roles.Employee)).Compile();
        Func<Expense, bool> auditorFilter = ExpenseAccess.ReadFilter(ExpenseTestData.CreateUser(Roles.Admin, Roles.Auditor)).Compile();
        Expense own = ExpenseTestData.CreateExpense((ExpenseStatus)status);
        Expense other = CreateOtherExpense((ExpenseStatus)status);

        Assert.IsTrue(employeeFilter(own));
        Assert.IsFalse(employeeFilter(other));
        Assert.IsTrue(auditorFilter(own));
        Assert.IsTrue(auditorFilter(other));
    }

    /// <summary>O helper recusa um principal anônimo sem roles antes de consultar dados.</summary>
    [TestMethod]
    public void ReadFilter_AnonymousWithoutRoles_IsForbidden()
    {
        ClaimsPrincipal user = new(new ClaimsIdentity());

        DomainException error = Assert.ThrowsExactly<DomainException>(() => ExpenseAccess.ReadFilter(user));

        Assert.AreEqual(DomainErrorType.Forbidden, error.Type);
    }

    /// <summary>Approver pode decidir um Submitted alheio sem que o helper altere a entidade.</summary>
    /// <param name="multipleRoles">Indica se o ator acumula outras roles.</param>
    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    public void EnsureCanDecide_ApproverOnAnotherSubmitted_IsAllowed(int multipleRoles)
    {
        ClaimsPrincipal user = multipleRoles == 1
            ? ExpenseTestData.CreateUser(Roles.Approver, Roles.Employee, Roles.Auditor, Roles.Finance, Roles.Admin)
            : ExpenseTestData.CreateUser(Roles.Approver);
        Expense expense = CreateOtherExpense(ExpenseStatus.Submitted);

        ExpenseAccess.EnsureCanDecide(user, expense);

        AssertNotChanged(expense, ExpenseStatus.Submitted);
    }

    /// <summary>Sem Approver, nenhuma outra role permite aprovar ou reprovar.</summary>
    /// <param name="role">Role indevida, ou null para nenhuma role.</param>
    [TestMethod]
    [DataRow(Roles.Employee)]
    [DataRow(Roles.Finance)]
    [DataRow(Roles.Auditor)]
    [DataRow(Roles.Admin)]
    [DataRow(null)]
    public void EnsureCanDecide_WithoutApprover_IsForbidden(string? role)
    {
        Expense expense = CreateOtherExpense(ExpenseStatus.Submitted);

        DomainException error = Assert.ThrowsExactly<DomainException>(() => ExpenseAccess.EnsureCanDecide(CreateUserWithOptionalRole(role), expense));

        Assert.AreEqual(DomainErrorType.Forbidden, error.Type);
        AssertNotChanged(expense, ExpenseStatus.Submitted);
    }

    /// <summary>Acumular roles nunca permite decidir sobre o próprio reembolso.</summary>
    /// <param name="multipleRoles">Indica se o ator acumula outras roles.</param>
    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    public void EnsureCanDecide_OwnExpense_IsForbidden(int multipleRoles)
    {
        ClaimsPrincipal user = multipleRoles == 1
            ? ExpenseTestData.CreateUser(Roles.Employee, Roles.Approver, Roles.Finance, Roles.Auditor, Roles.Admin)
            : ExpenseTestData.CreateUser(Roles.Approver);
        Expense expense = ExpenseTestData.CreateExpense(ExpenseStatus.Submitted);

        DomainException error = Assert.ThrowsExactly<DomainException>(() => ExpenseAccess.EnsureCanDecide(user, expense));

        Assert.AreEqual(DomainErrorType.Forbidden, error.Type);
        AssertNotChanged(expense, ExpenseStatus.Submitted);
    }

    /// <summary>Uma decisão exige Submitted mesmo para um Approver autorizado.</summary>
    /// <param name="status">Estado incompatível com a decisão.</param>
    [TestMethod]
    [DataRow((int)ExpenseStatus.Draft)]
    [DataRow((int)ExpenseStatus.Approved)]
    [DataRow((int)ExpenseStatus.Rejected)]
    [DataRow((int)ExpenseStatus.Paid)]
    public void EnsureCanDecide_NonSubmitted_IsConflict(int status)
    {
        Expense expense = CreateOtherExpense((ExpenseStatus)status);

        DomainException error = Assert.ThrowsExactly<DomainException>(() => ExpenseAccess.EnsureCanDecide(ExpenseTestData.CreateUser(Roles.Approver), expense));

        Assert.AreEqual(DomainErrorType.Conflict, error.Type);
        AssertNotChanged(expense, (ExpenseStatus)status);
    }

    /// <summary>A regra futura de pagamento admite Finance para um Approved alheio.</summary>
    /// <param name="multipleRoles">Indica se o ator acumula outras roles.</param>
    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    public void EnsureCanPay_FinanceOnAnotherApproved_IsAllowedWithoutPaying(int multipleRoles)
    {
        ClaimsPrincipal user = multipleRoles == 1
            ? ExpenseTestData.CreateUser(Roles.Finance, Roles.Employee, Roles.Auditor, Roles.Admin)
            : ExpenseTestData.CreateUser(Roles.Finance);
        Expense expense = CreateOtherExpense(ExpenseStatus.Approved);

        ExpenseAccess.EnsureCanPay(user, expense);

        AssertNotChanged(expense, ExpenseStatus.Approved);
        Assert.IsNull(expense.Payment);
    }

    /// <summary>Somente Finance concede a permissão funcional de pagamento.</summary>
    /// <param name="role">Role indevida, ou null para nenhuma role.</param>
    [TestMethod]
    [DataRow(Roles.Employee)]
    [DataRow(Roles.Approver)]
    [DataRow(Roles.Auditor)]
    [DataRow(Roles.Admin)]
    [DataRow(null)]
    public void EnsureCanPay_WithoutFinance_IsForbidden(string? role)
    {
        Expense expense = CreateOtherExpense(ExpenseStatus.Approved);

        DomainException error = Assert.ThrowsExactly<DomainException>(() => ExpenseAccess.EnsureCanPay(CreateUserWithOptionalRole(role), expense));

        Assert.AreEqual(DomainErrorType.Forbidden, error.Type);
        AssertNotChanged(expense, ExpenseStatus.Approved);
        Assert.IsNull(expense.Payment);
    }

    /// <summary>Finance não paga despesa própria, inclusive com Employee, Auditor e Admin.</summary>
    /// <param name="multipleRoles">Indica se o ator acumula outras roles.</param>
    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    public void EnsureCanPay_OwnExpense_IsForbidden(int multipleRoles)
    {
        ClaimsPrincipal user = multipleRoles == 1
            ? ExpenseTestData.CreateUser(Roles.Employee, Roles.Finance, Roles.Auditor, Roles.Admin)
            : ExpenseTestData.CreateUser(Roles.Finance);
        Expense expense = ExpenseTestData.CreateExpense(ExpenseStatus.Approved);

        DomainException error = Assert.ThrowsExactly<DomainException>(() => ExpenseAccess.EnsureCanPay(user, expense));

        Assert.AreEqual(DomainErrorType.Forbidden, error.Type);
        AssertNotChanged(expense, ExpenseStatus.Approved);
        Assert.IsNull(expense.Payment);
    }

    /// <summary>A regra futura de pagamento recusa todos os estados diferentes de Approved.</summary>
    /// <param name="status">Estado incompatível com o pagamento.</param>
    [TestMethod]
    [DataRow((int)ExpenseStatus.Draft)]
    [DataRow((int)ExpenseStatus.Submitted)]
    [DataRow((int)ExpenseStatus.Rejected)]
    [DataRow((int)ExpenseStatus.Paid)]
    public void EnsureCanPay_NonApproved_IsConflict(int status)
    {
        Expense expense = CreateOtherExpense((ExpenseStatus)status);

        DomainException error = Assert.ThrowsExactly<DomainException>(() => ExpenseAccess.EnsureCanPay(ExpenseTestData.CreateUser(Roles.Finance), expense));

        Assert.AreEqual(DomainErrorType.Conflict, error.Type);
        AssertNotChanged(expense, (ExpenseStatus)status);
        Assert.IsNull(expense.Payment);
    }

    private static Expense CreateOtherExpense(ExpenseStatus status)
    {
        Expense expense = ExpenseTestData.CreateExpense(status);
        expense.OwnerId = "employee-2";
        return expense;
    }

    private static ClaimsPrincipal CreateUserWithOptionalRole(string? role)
    {
        return role is null ? ExpenseTestData.CreateUser() : ExpenseTestData.CreateUser(role);
    }

    private static void AssertNotChanged(Expense expense, ExpenseStatus expectedStatus)
    {
        Assert.AreEqual(expectedStatus, expense.Status);
        Assert.HasCount(1, expense.History);
        Assert.AreEqual(ExpenseAction.Created, expense.History.Single().Action);
    }
}
