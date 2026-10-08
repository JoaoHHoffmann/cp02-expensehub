using System;
using System.ComponentModel.DataAnnotations;

namespace ExpenseHub.Api.Contracts;

internal sealed class ExpenseRequest
{
    [Required]
    [Range(1, int.MaxValue)]
    public int? CategoryId { get; set; }

    [Required]
    [StringLength(500, MinimumLength = 10)]
    public string? Description { get; set; }

    [Required]
    [Range(0.01, 2147483647.0)]
    public decimal? Amount { get; set; }

    [Required]
    public DateOnly? ExpenseDate { get; set; }
}
