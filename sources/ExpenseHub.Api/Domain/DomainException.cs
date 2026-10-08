using System;

namespace ExpenseHub.Api.Domain;

internal sealed class DomainException : Exception
{
    public DomainException(DomainErrorType type, string message)
        : base(message)
    {
        Type = type;
    }

    public DomainErrorType Type { get; }
}
