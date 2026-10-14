using System;
using System.Linq;
using ExpenseHub.Api.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests;

/// <summary>Verifica envio e preserva as regras existentes de criação e edição.</summary>
[TestClass]
public sealed class ExpenseSubmissionTests
{
    /// <summary>O proprietário envia um rascunho com histórico completo.</summary>
    [TestMethod]
    public void Submit_OwnDraft_RecordsTransitionWithActorAndUtcTime()
    {
        Expense expense = ExpenseTestData.CreateExpense();
        DateTime submittedAtUtc = ExpenseTestData.NowUtc.AddMinutes(5);

        expense.Submit(ExpenseTestData.OwnerId, submittedAtUtc);

        Assert.AreEqual(ExpenseStatus.Submitted, expense.Status);
        Assert.HasCount(2, expense.History);
        ExpenseHistory history = expense.History.Last();
        Assert.AreEqual(ExpenseAction.Submitted, history.Action);
        Assert.AreEqual(expense.Id, history.ExpenseId);
        Assert.AreEqual(ExpenseTestData.OwnerId, history.ActorId);
        Assert.AreEqual(submittedAtUtc, history.OccurredAtUtc);
        Assert.AreEqual(DateTimeKind.Utc, history.OccurredAtUtc.Kind);
        Assert.AreEqual(ExpenseStatus.Draft, history.PreviousStatus);
        Assert.AreEqual(ExpenseStatus.Submitted, history.NewStatus);
        Assert.IsNull(history.Justification);
        Assert.IsNull(history.Changes);
    }

    /// <summary>Outro proprietário não pode enviar o reembolso.</summary>
    [TestMethod]
    public void Submit_AnotherOwner_IsForbiddenWithoutChangingExpenseOrHistory()
    {
        Expense expense = ExpenseTestData.CreateExpense();

        DomainException error = Assert.ThrowsExactly<DomainException>(() => expense.Submit("employee-2", ExpenseTestData.NowUtc));

        Assert.AreEqual(DomainErrorType.Forbidden, error.Type);
        Assert.AreEqual(ExpenseStatus.Draft, expense.Status);
        Assert.HasCount(1, expense.History);
    }

    /// <summary>Todos os estados posteriores a Draft impedem novo envio.</summary>
    /// <param name="status">Estado incompatível com o envio.</param>
    [TestMethod]
    [DataRow((int)ExpenseStatus.Submitted)]
    [DataRow((int)ExpenseStatus.Approved)]
    [DataRow((int)ExpenseStatus.Rejected)]
    [DataRow((int)ExpenseStatus.Paid)]
    public void Submit_NonDraft_IsConflictWithoutChangingExpenseOrHistory(int status)
    {
        Expense expense = ExpenseTestData.CreateExpense((ExpenseStatus)status);

        DomainException error = Assert.ThrowsExactly<DomainException>(() => expense.Submit(ExpenseTestData.OwnerId, ExpenseTestData.NowUtc));

        Assert.AreEqual(DomainErrorType.Conflict, error.Type);
        Assert.AreEqual((ExpenseStatus)status, expense.Status);
        Assert.HasCount(1, expense.History);
    }

    /// <summary>A repetição retorna conflito sem acrescentar outro histórico.</summary>
    [TestMethod]
    public void Submit_RepeatedSubmission_IsConflictAndDoesNotDuplicateHistory()
    {
        Expense expense = ExpenseTestData.CreateExpense();
        expense.Submit(ExpenseTestData.OwnerId, ExpenseTestData.NowUtc);

        DomainException error = Assert.ThrowsExactly<DomainException>(() => expense.Submit(ExpenseTestData.OwnerId, ExpenseTestData.NowUtc.AddMinutes(1)));

        Assert.AreEqual(DomainErrorType.Conflict, error.Type);
        Assert.AreEqual(ExpenseStatus.Submitted, expense.Status);
        Assert.HasCount(2, expense.History);
        Assert.HasCount(1, expense.History.Where(h => h.Action == ExpenseAction.Submitted));
        Assert.AreEqual(ExpenseTestData.NowUtc, expense.History.Last().OccurredAtUtc);
    }

    /// <summary>A criação conserva identidade, normaliza descrição e registra Draft.</summary>
    [TestMethod]
    public void Create_ValidDetails_GeneratesDraftAndInitialHistory()
    {
        Expense expense = Expense.Create(ExpenseTestData.OwnerId, 1, "  Almoço com cliente  ", 50m, new DateOnly(2026, 10, 1), ExpenseTestData.NowUtc);

        Assert.AreNotEqual(Guid.Empty, expense.Id);
        Assert.AreEqual(ExpenseTestData.OwnerId, expense.OwnerId);
        Assert.AreEqual("Almoço com cliente", expense.Description);
        Assert.AreEqual(ExpenseStatus.Draft, expense.Status);
        Assert.AreEqual(ExpenseTestData.NowUtc, expense.CreatedAtUtc);
        ExpenseHistory history = expense.History.Single();
        Assert.AreEqual(ExpenseAction.Created, history.Action);
        Assert.AreEqual(expense.Id, history.ExpenseId);
        Assert.AreEqual(ExpenseTestData.OwnerId, history.ActorId);
        Assert.AreEqual(ExpenseTestData.NowUtc, history.OccurredAtUtc);
        Assert.IsNull(history.PreviousStatus);
        Assert.AreEqual(ExpenseStatus.Draft, history.NewStatus);
    }

    /// <summary>Descrições fora dos limites são recusadas.</summary>
    /// <param name="length">Comprimento inválido da descrição.</param>
    [TestMethod]
    [DataRow(0)]
    [DataRow(9)]
    [DataRow(501)]
    public void Create_InvalidDescription_IsValidationError(int length)
    {
        DomainException error = Assert.ThrowsExactly<DomainException>(() => Expense.Create(
            ExpenseTestData.OwnerId, 1, new string('a', length), 50m, new DateOnly(2026, 10, 1), ExpenseTestData.NowUtc));

        Assert.AreEqual(DomainErrorType.Validation, error.Type);
    }

    /// <summary>Uma descrição composta apenas de espaços é inválida.</summary>
    [TestMethod]
    public void Create_WhitespaceDescription_IsValidationError()
    {
        DomainException error = Assert.ThrowsExactly<DomainException>(() => Expense.Create(
            ExpenseTestData.OwnerId, 1, new string(' ', 20), 50m, new DateOnly(2026, 10, 1), ExpenseTestData.NowUtc));

        Assert.AreEqual(DomainErrorType.Validation, error.Type);
    }

    /// <summary>Valores fora dos limites monetários são recusados.</summary>
    /// <param name="amount">Valor inválido do reembolso.</param>
    [TestMethod]
    [DataRow(0L)]
    [DataRow(-1L)]
    [DataRow(2147483648L)]
    public void Create_InvalidAmount_IsValidationError(long amount)
    {
        DomainException error = Assert.ThrowsExactly<DomainException>(() => Expense.Create(
            ExpenseTestData.OwnerId, 1, "Almoço com cliente", amount, new DateOnly(2026, 10, 1), ExpenseTestData.NowUtc));

        Assert.AreEqual(DomainErrorType.Validation, error.Type);
    }

    /// <summary>A data da despesa não pode ultrapassar a data do servidor.</summary>
    [TestMethod]
    public void Create_FutureExpenseDate_IsValidationError()
    {
        DateOnly tomorrow = DateOnly.FromDateTime(ExpenseTestData.NowUtc).AddDays(1);

        DomainException error = Assert.ThrowsExactly<DomainException>(() => Expense.Create(
            ExpenseTestData.OwnerId, 1, "Almoço com cliente", 50m, tomorrow, ExpenseTestData.NowUtc));

        Assert.AreEqual(DomainErrorType.Validation, error.Type);
    }

    /// <summary>A edição registra os campos alterados sem mudar identidade ou estado.</summary>
    [TestMethod]
    public void Update_OwnDraft_PreservesIdentityAndRecordsChanges()
    {
        Expense expense = ExpenseTestData.CreateExpense();
        Guid originalId = expense.Id;
        DateTime updatedAtUtc = ExpenseTestData.NowUtc.AddMinutes(5);

        expense.Update(ExpenseTestData.OwnerId, 2, "  Transporte para reunião  ", 75m, new DateOnly(2026, 10, 2), updatedAtUtc);

        Assert.AreEqual(originalId, expense.Id);
        Assert.AreEqual(ExpenseTestData.OwnerId, expense.OwnerId);
        Assert.AreEqual(ExpenseTestData.NowUtc, expense.CreatedAtUtc);
        Assert.AreEqual(ExpenseStatus.Draft, expense.Status);
        Assert.AreEqual(2, expense.CategoryId);
        Assert.AreEqual("Transporte para reunião", expense.Description);
        Assert.AreEqual(75m, expense.Amount);
        Assert.AreEqual(new DateOnly(2026, 10, 2), expense.ExpenseDate);
        Assert.HasCount(2, expense.History);
        ExpenseHistory history = expense.History.Last();
        Assert.AreEqual(ExpenseAction.Updated, history.Action);
        Assert.AreEqual(originalId, history.ExpenseId);
        Assert.AreEqual(ExpenseTestData.OwnerId, history.ActorId);
        Assert.AreEqual(updatedAtUtc, history.OccurredAtUtc);
        Assert.AreEqual(ExpenseStatus.Draft, history.PreviousStatus);
        Assert.AreEqual(ExpenseStatus.Draft, history.NewStatus);
        Assert.IsNotNull(history.Changes);
        StringAssert.Contains(history.Changes, "Categoria: 1 -> 2");
        StringAssert.Contains(history.Changes, "Valor: 50 -> 75");
        StringAssert.Contains(history.Changes, "2026-10-01 -> 2026-10-02");
        StringAssert.Contains(history.Changes, "Almoço com cliente");
        StringAssert.Contains(history.Changes, "Transporte para reunião");
    }

    /// <summary>Salvar os mesmos dados não cria histórico adicional.</summary>
    [TestMethod]
    public void Update_UnchangedDetails_DoesNotDuplicateHistory()
    {
        Expense expense = ExpenseTestData.CreateExpense();

        expense.Update(ExpenseTestData.OwnerId, expense.CategoryId, expense.Description, expense.Amount, expense.ExpenseDate, ExpenseTestData.NowUtc.AddMinutes(5));

        Assert.HasCount(1, expense.History);
        Assert.AreEqual(ExpenseStatus.Draft, expense.Status);
    }

    /// <summary>Outro proprietário não pode alterar os dados nem o histórico.</summary>
    [TestMethod]
    public void Update_AnotherOwner_IsForbiddenWithoutChangingExpenseOrHistory()
    {
        Expense expense = ExpenseTestData.CreateExpense();

        DomainException error = Assert.ThrowsExactly<DomainException>(() => expense.Update(
            "employee-2", 2, "Transporte para reunião", 75m, new DateOnly(2026, 10, 2), ExpenseTestData.NowUtc));

        Assert.AreEqual(DomainErrorType.Forbidden, error.Type);
        Assert.AreEqual("Almoço com cliente", expense.Description);
        Assert.AreEqual(50m, expense.Amount);
        Assert.HasCount(1, expense.History);
    }

    /// <summary>Todos os estados posteriores a Draft impedem edição.</summary>
    /// <param name="status">Estado incompatível com a edição.</param>
    [TestMethod]
    [DataRow((int)ExpenseStatus.Submitted)]
    [DataRow((int)ExpenseStatus.Approved)]
    [DataRow((int)ExpenseStatus.Rejected)]
    [DataRow((int)ExpenseStatus.Paid)]
    public void Update_NonDraft_IsConflictWithoutChangingExpenseOrHistory(int status)
    {
        Expense expense = ExpenseTestData.CreateExpense((ExpenseStatus)status);

        DomainException error = Assert.ThrowsExactly<DomainException>(() => expense.Update(
            ExpenseTestData.OwnerId, 2, "Transporte para reunião", 75m, new DateOnly(2026, 10, 2), ExpenseTestData.NowUtc));

        Assert.AreEqual(DomainErrorType.Conflict, error.Type);
        Assert.AreEqual((ExpenseStatus)status, expense.Status);
        Assert.AreEqual("Almoço com cliente", expense.Description);
        Assert.AreEqual(50m, expense.Amount);
        Assert.HasCount(1, expense.History);
    }

    /// <summary>Dados inválidos são recusados antes de alterar a entidade.</summary>
    [TestMethod]
    public void Update_InvalidDetails_DoesNotChangeExpenseOrHistory()
    {
        Expense expense = ExpenseTestData.CreateExpense();

        DomainException error = Assert.ThrowsExactly<DomainException>(() => expense.Update(
            ExpenseTestData.OwnerId, 2, "Curta", 75m, new DateOnly(2026, 10, 2), ExpenseTestData.NowUtc));

        Assert.AreEqual(DomainErrorType.Validation, error.Type);
        Assert.AreEqual(1, expense.CategoryId);
        Assert.AreEqual("Almoço com cliente", expense.Description);
        Assert.AreEqual(50m, expense.Amount);
        Assert.AreEqual(new DateOnly(2026, 10, 1), expense.ExpenseDate);
        Assert.HasCount(1, expense.History);
    }
}
