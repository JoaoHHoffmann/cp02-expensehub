using System.Collections.Generic;

namespace ExpenseHub.Api.Contracts;

internal sealed record UserResponse(string Id, string? Email, IEnumerable<string> Roles);
