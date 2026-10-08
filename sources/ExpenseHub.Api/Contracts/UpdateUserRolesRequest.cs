using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ExpenseHub.Api.Contracts;

internal sealed class UpdateUserRolesRequest
{
    [Required]
    [MaxLength(5)]
    public List<string> Roles { get; set; } = [];
}
