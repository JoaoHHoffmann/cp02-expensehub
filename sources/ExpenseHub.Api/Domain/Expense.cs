using System;
using System.Collections.Generic;
using System.Globalization;

namespace ExpenseHub.Api.Domain;

internal sealed class Expense
{
    public Guid Id { get; set; }

    public string OwnerId { get; set; } = string.Empty;

    public int CategoryId { get; set; }

    public ExpenseCategory? Category { get; set; }

    public string Description { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public DateOnly ExpenseDate { get; set; }

    public ExpenseStatus Status { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public ICollection<ExpenseHistory> History { get; set; } = [];

    public PaymentRecord? Payment { get; set; }

    public static Expense Create(string ownerId, int categoryId, string description, decimal amount, DateOnly expenseDate, DateTime nowUtc)
    {
        ValidateDetails(description, amount, expenseDate, nowUtc);

        Expense expense = new()
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            CategoryId = categoryId,
            Description = description.Trim(),
            Amount = amount,
            ExpenseDate = expenseDate,
            Status = ExpenseStatus.Draft,
            CreatedAtUtc = nowUtc,
        };

        expense.AddHistory(ExpenseAction.Created, ownerId, nowUtc, null, ExpenseStatus.Draft);
        return expense;
    }

    public void Update(string actorId, int categoryId, string description, decimal amount, DateOnly expenseDate, DateTime nowUtc)
    {
        EnsureOwner(actorId);
        EnsureStatus(ExpenseStatus.Draft, "Somente rascunhos podem ser editados.");
        ValidateDetails(description, amount, expenseDate, nowUtc);

        string newDescription = description.Trim();
        List<string> changes = [];
        if (CategoryId != categoryId)
        {
            changes.Add(string.Create(CultureInfo.InvariantCulture, $"Categoria: {CategoryId} -> {categoryId}"));
        }

        if (Description != newDescription)
        {
            changes.Add($"Descrição: '{Description}' -> '{newDescription}'");
        }

        if (Amount != amount)
        {
            changes.Add(string.Create(CultureInfo.InvariantCulture, $"Valor: {Amount} -> {amount}"));
        }

        if (ExpenseDate != expenseDate)
        {
            changes.Add(string.Create(CultureInfo.InvariantCulture, $"Data: {ExpenseDate:yyyy-MM-dd} -> {expenseDate:yyyy-MM-dd}"));
        }

        if (changes.Count == 0)
        {
            return;
        }

        CategoryId = categoryId;
        Description = newDescription;
        Amount = amount;
        ExpenseDate = expenseDate;
        AddHistory(ExpenseAction.Updated, actorId, nowUtc, ExpenseStatus.Draft, ExpenseStatus.Draft, changes: string.Join("; ", changes));
    }

    private static void ValidateDetails(string description, decimal amount, DateOnly expenseDate, DateTime nowUtc)
    {
        int length = description.Trim().Length;
        if (length < 10 || length > 500)
        {
            throw new DomainException(DomainErrorType.Validation, "A descrição deve ter entre 10 e 500 caracteres.");
        }

        if (amount < 0.01m || amount > int.MaxValue)
        {
            throw new DomainException(DomainErrorType.Validation, "O valor deve estar entre R$ 0,01 e R$ 2.147.483.647,00.");
        }

        if (expenseDate > DateOnly.FromDateTime(nowUtc))
        {
            throw new DomainException(DomainErrorType.Validation, "A data da despesa não pode ser futura.");
        }
    }

    private void AddHistory(
        ExpenseAction action,
        string actorId,
        DateTime nowUtc,
        ExpenseStatus? previousStatus,
        ExpenseStatus newStatus,
        string? justification = null,
        string? changes = null)
    {
        History.Add(new ExpenseHistory
        {
            ExpenseId = Id,
            Action = action,
            ActorId = actorId,
            OccurredAtUtc = nowUtc,
            PreviousStatus = previousStatus,
            NewStatus = newStatus,
            Justification = justification,
            Changes = changes,
        });
    }

    private void EnsureOwner(string actorId)
    {
        if (OwnerId != actorId)
        {
            throw new DomainException(DomainErrorType.Forbidden, "Somente o proprietário pode alterar este reembolso.");
        }
    }

    private void EnsureStatus(ExpenseStatus expected, string message)
    {
        if (Status != expected)
        {
            throw new DomainException(DomainErrorType.Conflict, message);
        }
    }
}
