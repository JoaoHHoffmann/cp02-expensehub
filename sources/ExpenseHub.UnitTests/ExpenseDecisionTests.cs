using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.Json;
using ExpenseHub.Api.Contracts;
using ExpenseHub.Api.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests;

/// <summary>Verifica decisões, justificativas e histórico sem dependências externas.</summary>
[TestClass]
public sealed class ExpenseDecisionTests
{
    private const string ApproverId = "approver-1";
    private const string Justification = "Documento fiscal ilegível.";

    /// <summary>A aprovação muda Submitted para Approved e registra ator, estados e UTC.</summary>
    [TestMethod]
    public void Approve_SubmittedByAnotherOwner_RecordsCompleteTransition()
    {
        Expense expense = CreateSubmittedExpense();
        DateTime decidedAtUtc = ExpenseTestData.NowUtc.AddMinutes(2);

        expense.Approve(ApproverId, decidedAtUtc);

        Assert.AreEqual(ExpenseStatus.Approved, expense.Status);
        Assert.AreEqual(ExpenseTestData.OwnerId, expense.OwnerId);
        Assert.HasCount(3, expense.History);
        ExpenseHistory history = expense.History.Last();
        Assert.AreEqual(ExpenseAction.Approved, history.Action);
        AssertDecisionHistory(expense, history, ExpenseStatus.Approved, decidedAtUtc);
        Assert.IsNull(history.Justification);
        Assert.IsNull(history.Changes);
    }

    /// <summary>A reprovação registra a justificativa junto da transição para Rejected.</summary>
    [TestMethod]
    public void Reject_SubmittedByAnotherOwner_RecordsCompleteTransition()
    {
        Expense expense = CreateSubmittedExpense();
        DateTime decidedAtUtc = ExpenseTestData.NowUtc.AddMinutes(2);

        expense.Reject(ApproverId, Justification, decidedAtUtc);

        Assert.AreEqual(ExpenseStatus.Rejected, expense.Status);
        Assert.AreEqual(ExpenseTestData.OwnerId, expense.OwnerId);
        Assert.HasCount(3, expense.History);
        ExpenseHistory history = expense.History.Last();
        Assert.AreEqual(ExpenseAction.Rejected, history.Action);
        AssertDecisionHistory(expense, history, ExpenseStatus.Rejected, decidedAtUtc);
        Assert.AreEqual(Justification, history.Justification);
        Assert.IsNull(history.Changes);
    }

    /// <summary>O proprietário nunca aprova nem reprova o próprio reembolso.</summary>
    /// <param name="decision">Decisão tentada pelo proprietário.</param>
    [TestMethod]
    [DataRow("approve")]
    [DataRow("reject")]
    public void Decision_OwnExpense_IsForbiddenWithoutChangingHistory(string decision)
    {
        Expense expense = CreateSubmittedExpense();

        DomainException error = Assert.ThrowsExactly<DomainException>(() => ApplyDecision(expense, decision, ExpenseTestData.OwnerId));

        Assert.AreEqual(DomainErrorType.Forbidden, error.Type);
        AssertUnchanged(expense, ExpenseStatus.Submitted, 2);
    }

    /// <summary>Ambas as decisões exigem Submitted e recusam todos os outros estados.</summary>
    /// <param name="decision">Decisão tentada.</param>
    /// <param name="status">Estado incompatível com a decisão.</param>
    [TestMethod]
    [DataRow("approve", (int)ExpenseStatus.Draft)]
    [DataRow("approve", (int)ExpenseStatus.Approved)]
    [DataRow("approve", (int)ExpenseStatus.Rejected)]
    [DataRow("approve", (int)ExpenseStatus.Paid)]
    [DataRow("reject", (int)ExpenseStatus.Draft)]
    [DataRow("reject", (int)ExpenseStatus.Approved)]
    [DataRow("reject", (int)ExpenseStatus.Rejected)]
    [DataRow("reject", (int)ExpenseStatus.Paid)]
    public void Decision_NonSubmitted_IsConflictWithoutChangingHistory(string decision, int status)
    {
        Expense expense = ExpenseTestData.CreateExpense((ExpenseStatus)status);

        DomainException error = Assert.ThrowsExactly<DomainException>(() => ApplyDecision(expense, decision, ApproverId));

        Assert.AreEqual(DomainErrorType.Conflict, error.Type);
        AssertUnchanged(expense, (ExpenseStatus)status, 1);
    }

    /// <summary>Repetir ou contradizer uma decisão não muda o resultado nem duplica histórico.</summary>
    /// <param name="firstDecision">Decisão original válida.</param>
    /// <param name="secondDecision">Decisão repetida ou contraditória.</param>
    /// <param name="expectedStatus">Estado preservado após o conflito.</param>
    [TestMethod]
    [DataRow("approve", "approve", (int)ExpenseStatus.Approved)]
    [DataRow("approve", "reject", (int)ExpenseStatus.Approved)]
    [DataRow("reject", "reject", (int)ExpenseStatus.Rejected)]
    [DataRow("reject", "approve", (int)ExpenseStatus.Rejected)]
    public void Decision_AlreadyDecided_IsConflictWithoutDuplicatingHistory(string firstDecision, string secondDecision, int expectedStatus)
    {
        Expense expense = CreateSubmittedExpense();
        ApplyDecision(expense, firstDecision, ApproverId);
        ExpenseHistory originalDecision = expense.History.Last();

        DomainException error = Assert.ThrowsExactly<DomainException>(() => ApplyDecision(expense, secondDecision, "approver-2"));

        Assert.AreEqual(DomainErrorType.Conflict, error.Type);
        AssertUnchanged(expense, (ExpenseStatus)expectedStatus, 3);
        Assert.AreSame(originalDecision, expense.History.Last());
        Assert.HasCount(1, expense.History.Where(h => h.Action is ExpenseAction.Approved or ExpenseAction.Rejected));
        Assert.AreEqual(ApproverId, originalDecision.ActorId);
    }

    /// <summary>Ausência, espaços e texto curto depois de Trim impedem a reprovação.</summary>
    /// <param name="justification">Justificativa inválida.</param>
    [TestMethod]
    [DataRow(null)]
    [DataRow(" ")]
    [DataRow("                    ")]
    [DataRow("Curta")]
    [DataRow(" 123456789 ")]
    [DataRow("\t123456789\n")]
    public void Reject_InvalidJustification_IsValidationErrorWithoutChangingHistory(string? justification)
    {
        Expense expense = CreateSubmittedExpense();

        DomainException error = Assert.ThrowsExactly<DomainException>(() => expense.Reject(ApproverId, justification, ExpenseTestData.NowUtc));

        Assert.AreEqual(DomainErrorType.Validation, error.Type);
        AssertUnchanged(expense, ExpenseStatus.Submitted, 2);
    }

    /// <summary>Texto vazio ou fora dos limites de comprimento é recusado.</summary>
    /// <param name="length">Comprimento inválido da justificativa.</param>
    [TestMethod]
    [DataRow(0)]
    [DataRow(9)]
    [DataRow(501)]
    public void Reject_JustificationOutsideLengthLimits_IsValidationError(int length)
    {
        Expense expense = CreateSubmittedExpense();

        DomainException error = Assert.ThrowsExactly<DomainException>(() => expense.Reject(ApproverId, new string('a', length), ExpenseTestData.NowUtc));

        Assert.AreEqual(DomainErrorType.Validation, error.Type);
        AssertUnchanged(expense, ExpenseStatus.Submitted, 2);
    }

    /// <summary>Os limites inclusivos de dez e quinhentos caracteres são aceitos.</summary>
    /// <param name="length">Comprimento válido no limite.</param>
    [TestMethod]
    [DataRow(10)]
    [DataRow(500)]
    public void Reject_JustificationAtLengthLimits_IsAcceptedAndPersisted(int length)
    {
        Expense expense = CreateSubmittedExpense();
        string justification = new('a', length);

        expense.Reject(ApproverId, justification, ExpenseTestData.NowUtc);

        Assert.AreEqual(ExpenseStatus.Rejected, expense.Status);
        Assert.HasCount(3, expense.History);
        Assert.AreEqual(justification, expense.History.Last().Justification);
    }

    /// <summary>Espaços externos são removidos antes de persistir a justificativa.</summary>
    [TestMethod]
    public void Reject_JustificationWithOuterWhitespace_PersistsTrimmedText()
    {
        Expense expense = CreateSubmittedExpense();

        expense.Reject(ApproverId, "  Sem recibo  ", ExpenseTestData.NowUtc);

        Assert.AreEqual(ExpenseStatus.Rejected, expense.Status);
        Assert.AreEqual("Sem recibo", expense.History.Last().Justification);
    }

    /// <summary>A validação declarativa recusa justificativa ausente, em branco ou curta.</summary>
    /// <param name="justification">Valor inválido recebido pelo DTO.</param>
    [TestMethod]
    [DataRow(null)]
    [DataRow(" ")]
    [DataRow("                    ")]
    [DataRow("Curta")]
    public void RejectRequest_InvalidJustification_FailsDeclarativeValidation(string? justification)
    {
        RejectExpenseRequest request = new() { Justification = justification };
        List<ValidationResult> errors = [];

        bool valid = Validator.TryValidateObject(request, new ValidationContext(request), errors, validateAllProperties: true);

        Assert.IsFalse(valid);
        Assert.IsTrue(errors.Any(e => e.MemberNames.Contains(nameof(RejectExpenseRequest.Justification))));
    }

    /// <summary>O DTO rejeita texto vazio e os comprimentos fora do contrato.</summary>
    /// <param name="length">Comprimento inválido da justificativa.</param>
    [TestMethod]
    [DataRow(0)]
    [DataRow(9)]
    [DataRow(501)]
    public void RejectRequest_JustificationOutsideLengthLimits_FailsDeclarativeValidation(int length)
    {
        RejectExpenseRequest request = new() { Justification = new string('a', length) };
        List<ValidationResult> errors = [];

        bool valid = Validator.TryValidateObject(request, new ValidationContext(request), errors, validateAllProperties: true);

        Assert.IsFalse(valid);
        Assert.IsTrue(errors.Any(e => e.MemberNames.Contains(nameof(RejectExpenseRequest.Justification))));
    }

    /// <summary>A validação declarativa permite os limites inclusivos do contrato.</summary>
    /// <param name="length">Comprimento válido no limite.</param>
    [TestMethod]
    [DataRow(10)]
    [DataRow(500)]
    public void RejectRequest_JustificationAtLengthLimits_PassesDeclarativeValidation(int length)
    {
        RejectExpenseRequest request = new() { Justification = new string('a', length) };
        List<ValidationResult> errors = [];

        bool valid = Validator.TryValidateObject(request, new ValidationContext(request), errors, validateAllProperties: true);

        Assert.IsTrue(valid);
        Assert.IsEmpty(errors);
    }

    /// <summary>O DTO aceita apenas justificativa e ignora identidade, estado e horário enviados.</summary>
    [TestMethod]
    public void RejectRequest_SpoofedServerFields_AreIgnored()
    {
        const string json = """
            {
              "justification": "Documento fiscal ilegível.",
              "ownerId": "intruder",
              "actorId": "forged-actor",
              "status": "Paid",
              "occurredAtUtc": "2000-01-01T00:00:00Z"
            }
            """;
        JsonSerializerOptions options = new(JsonSerializerDefaults.Web);
        RejectExpenseRequest? request = JsonSerializer.Deserialize<RejectExpenseRequest>(json, options);
        Assert.IsNotNull(request);
        Assert.AreEqual(Justification, request.Justification);

        JsonElement serialized = JsonSerializer.SerializeToElement(request, options);

        Assert.HasCount(1, serialized.EnumerateObject());
        Assert.IsTrue(serialized.TryGetProperty("justification", out _));
        Assert.IsFalse(serialized.TryGetProperty("ownerId", out _));
        Assert.IsFalse(serialized.TryGetProperty("actorId", out _));
        Assert.IsFalse(serialized.TryGetProperty("status", out _));
        Assert.IsFalse(serialized.TryGetProperty("occurredAtUtc", out _));

        Expense expense = CreateSubmittedExpense();
        expense.Reject(ApproverId, request.Justification, ExpenseTestData.NowUtc);
        Assert.AreEqual(ExpenseTestData.OwnerId, expense.OwnerId);
        Assert.AreEqual(ExpenseStatus.Rejected, expense.Status);
        Assert.AreEqual(ApproverId, expense.History.Last().ActorId);
        Assert.AreEqual(ExpenseTestData.NowUtc, expense.History.Last().OccurredAtUtc);
    }

    private static Expense CreateSubmittedExpense()
    {
        Expense expense = ExpenseTestData.CreateExpense();
        expense.Submit(ExpenseTestData.OwnerId, ExpenseTestData.NowUtc.AddMinutes(1));
        return expense;
    }

    private static void ApplyDecision(Expense expense, string decision, string actorId)
    {
        if (decision == "approve")
        {
            expense.Approve(actorId, ExpenseTestData.NowUtc.AddMinutes(2));
        }
        else
        {
            expense.Reject(actorId, Justification, ExpenseTestData.NowUtc.AddMinutes(2));
        }
    }

    private static void AssertDecisionHistory(Expense expense, ExpenseHistory history, ExpenseStatus expectedStatus, DateTime decidedAtUtc)
    {
        Assert.AreEqual(expense.Id, history.ExpenseId);
        Assert.AreEqual(ApproverId, history.ActorId);
        Assert.AreEqual(decidedAtUtc, history.OccurredAtUtc);
        Assert.AreEqual(DateTimeKind.Utc, history.OccurredAtUtc.Kind);
        Assert.AreEqual(ExpenseStatus.Submitted, history.PreviousStatus);
        Assert.AreEqual(expectedStatus, history.NewStatus);
    }

    private static void AssertUnchanged(Expense expense, ExpenseStatus expectedStatus, int expectedHistoryCount)
    {
        Assert.AreEqual(expectedStatus, expense.Status);
        Assert.AreEqual(ExpenseTestData.OwnerId, expense.OwnerId);
        Assert.HasCount(expectedHistoryCount, expense.History);
        Assert.AreEqual("Almoço com cliente", expense.Description);
        Assert.AreEqual(50m, expense.Amount);
    }
}
