using System.ComponentModel.DataAnnotations;

namespace ExpenseHub.Api.Contracts;

internal sealed class RejectExpenseRequest
{
    [Required]
    [StringLength(500, MinimumLength = 10)]
    public string? Justification { get; set; }
}
