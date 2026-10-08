using System;

namespace ExpenseHub.Api.Domain;

internal sealed class PaymentRecord
{
    public Guid Id { get; set; }

    public Guid ExpenseId { get; set; }

    public string PaidById { get; set; } = string.Empty;

    public DateTime PaidAtUtc { get; set; }

    public decimal Amount { get; set; }
}
