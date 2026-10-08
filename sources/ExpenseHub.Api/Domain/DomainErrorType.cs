namespace ExpenseHub.Api.Domain;

internal enum DomainErrorType
{
    Validation = 0,
    Forbidden = 1,
    NotFound = 2,
    Conflict = 3,
}
