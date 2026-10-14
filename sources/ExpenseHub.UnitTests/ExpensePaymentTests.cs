using System;
using System.Linq;
using ExpenseHub.Api.Contracts;
using ExpenseHub.Api.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests;

/// <summary>Verifica o pagamento, o registro correspondente e o histórico sem dependências externas.</summary>
[TestClass]
public sealed class ExpensePaymentTests
{
    private const string FinanceId = "finance-1";

    /// <summary>O pagamento muda Approved para Paid e grava o registro com ator, horário e valor do servidor.</summary>
    [TestMethod]
    public void Pay_ApprovedByAnotherOwner_RecordsPaymentAndHistory()
    {
        Expense expense = CreateApprovedExpense();
        DateTime paidAtUtc = ExpenseTestData.NowUtc.AddMinutes(3);

        expense.Pay(FinanceId, paidAtUtc);

        Assert.AreEqual(ExpenseStatus.Paid, expense.Status);
        Assert.IsNotNull(expense.Payment);
        Assert.AreEqual(expense.Id, expense.Payment.ExpenseId);
        Assert.AreEqual(FinanceId, expense.Payment.PaidById);
        Assert.AreEqual(paidAtUtc, expense.Payment.PaidAtUtc);
        Assert.AreEqual(DateTimeKind.Utc, expense.Payment.PaidAtUtc.Kind);
        Assert.AreEqual(expense.Amount, expense.Payment.Amount);
        Assert.AreNotEqual(Guid.Empty, expense.Payment.Id);

        Assert.HasCount(4, expense.History);
        ExpenseHistory history = expense.History.Last();
        Assert.AreEqual(ExpenseAction.Paid, history.Action);
        Assert.AreEqual(FinanceId, history.ActorId);
        Assert.AreEqual(paidAtUtc, history.OccurredAtUtc);
        Assert.AreEqual(ExpenseStatus.Approved, history.PreviousStatus);
        Assert.AreEqual(ExpenseStatus.Paid, history.NewStatus);
        Assert.IsNull(history.Justification);
        Assert.IsNull(history.Changes);
    }

    /// <summary>O proprietário nunca paga o próprio reembolso.</summary>
    [TestMethod]
    public void Pay_OwnExpense_IsForbiddenWithoutChangingHistoryOrPayment()
    {
        Expense expense = CreateApprovedExpense();

        DomainException error = Assert.ThrowsExactly<DomainException>(() => expense.Pay(ExpenseTestData.OwnerId, ExpenseTestData.NowUtc));

        Assert.AreEqual(DomainErrorType.Forbidden, error.Type);
        AssertUnchanged(expense, ExpenseStatus.Approved, 3);
    }

    /// <summary>O pagamento exige Approved e recusa todos os outros estados.</summary>
    /// <param name="status">Estado incompatível com o pagamento.</param>
    [TestMethod]
    [DataRow((int)ExpenseStatus.Draft)]
    [DataRow((int)ExpenseStatus.Submitted)]
    [DataRow((int)ExpenseStatus.Rejected)]
    [DataRow((int)ExpenseStatus.Paid)]
    public void Pay_NonApproved_IsConflictWithoutChangingHistoryOrPayment(int status)
    {
        Expense expense = ExpenseTestData.CreateExpense((ExpenseStatus)status);

        DomainException error = Assert.ThrowsExactly<DomainException>(() => expense.Pay(FinanceId, ExpenseTestData.NowUtc));

        Assert.AreEqual(DomainErrorType.Conflict, error.Type);
        AssertUnchanged(expense, (ExpenseStatus)status, 1);
    }

    /// <summary>Repetir o pagamento retorna conflito sem duplicar registro ou histórico.</summary>
    [TestMethod]
    public void Pay_RepeatedPayment_IsConflictAndDoesNotDuplicatePaymentOrHistory()
    {
        Expense expense = CreateApprovedExpense();
        expense.Pay(FinanceId, ExpenseTestData.NowUtc.AddMinutes(3));
        PaymentRecord originalPayment = expense.Payment!;

        DomainException error = Assert.ThrowsExactly<DomainException>(() => expense.Pay("finance-2", ExpenseTestData.NowUtc.AddMinutes(4)));

        Assert.AreEqual(DomainErrorType.Conflict, error.Type);
        AssertUnchanged(expense, ExpenseStatus.Paid, 4);
        Assert.AreSame(originalPayment, expense.Payment);
        Assert.HasCount(1, expense.History.Where(h => h.Action == ExpenseAction.Paid));
    }

    /// <summary>O mapeamento inclui o pagamento somente quando o reembolso foi pago.</summary>
    [TestMethod]
    public void ExpenseResponse_FromEntity_IncludesPaymentOnlyWhenPaid()
    {
        Expense approved = CreateApprovedExpense();
        ExpenseResponse beforePayment = ExpenseResponse.FromEntity(approved);
        Assert.IsNull(beforePayment.Payment);

        DateTime paidAtUtc = ExpenseTestData.NowUtc.AddMinutes(3);
        approved.Pay(FinanceId, paidAtUtc);

        ExpenseResponse afterPayment = ExpenseResponse.FromEntity(approved);
        Assert.IsNotNull(afterPayment.Payment);
        Assert.AreEqual(FinanceId, afterPayment.Payment.PaidById);
        Assert.AreEqual(paidAtUtc, afterPayment.Payment.PaidAtUtc);
        Assert.AreEqual(approved.Amount, afterPayment.Payment.Amount);
    }

    /// <summary>O mapeamento do histórico preserva todos os campos registrados pelo domínio.</summary>
    [TestMethod]
    public void ExpenseHistoryResponse_FromEntity_MapsAllFields()
    {
        Expense expense = CreateApprovedExpense();
        DateTime paidAtUtc = ExpenseTestData.NowUtc.AddMinutes(3);
        expense.Pay(FinanceId, paidAtUtc);
        ExpenseHistory history = expense.History.Last();

        ExpenseHistoryResponse response = ExpenseHistoryResponse.FromEntity(history);

        Assert.AreEqual(history.Id, response.Id);
        Assert.AreEqual(history.ExpenseId, response.ExpenseId);
        Assert.AreEqual(ExpenseAction.Paid, response.Action);
        Assert.AreEqual(FinanceId, response.ActorId);
        Assert.AreEqual(paidAtUtc, response.OccurredAtUtc);
        Assert.AreEqual(ExpenseStatus.Approved, response.PreviousStatus);
        Assert.AreEqual(ExpenseStatus.Paid, response.NewStatus);
        Assert.IsNull(response.Justification);
        Assert.IsNull(response.Changes);
    }

    private static Expense CreateApprovedExpense()
    {
        Expense expense = ExpenseTestData.CreateExpense();
        expense.Submit(ExpenseTestData.OwnerId, ExpenseTestData.NowUtc.AddMinutes(1));
        expense.Approve("approver-1", ExpenseTestData.NowUtc.AddMinutes(2));
        return expense;
    }

    private static void AssertUnchanged(Expense expense, ExpenseStatus expectedStatus, int expectedHistoryCount)
    {
        Assert.AreEqual(expectedStatus, expense.Status);
        Assert.AreEqual(ExpenseTestData.OwnerId, expense.OwnerId);
        Assert.HasCount(expectedHistoryCount, expense.History);
    }
}
