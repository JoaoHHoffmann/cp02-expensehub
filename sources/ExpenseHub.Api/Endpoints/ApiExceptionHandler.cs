using System;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Domain;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseHub.Api.Endpoints;

internal sealed class ApiExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetailsService;

    public ApiExceptionHandler(IProblemDetailsService problemDetailsService)
    {
        _problemDetailsService = problemDetailsService;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        int? status = exception switch
        {
            DomainException { Type: DomainErrorType.Validation } => StatusCodes.Status400BadRequest,
            DomainException { Type: DomainErrorType.Forbidden } => StatusCodes.Status403Forbidden,
            DomainException { Type: DomainErrorType.NotFound } => StatusCodes.Status404NotFound,
            DomainException { Type: DomainErrorType.Conflict } => StatusCodes.Status409Conflict,
            BadHttpRequestException badRequest => badRequest.StatusCode,
            _ => null,
        };

        if (status is null)
        {
            return false;
        }

        httpContext.Response.StatusCode = status.Value;
        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails { Status = status.Value, Title = exception.Message },
        });
    }
}
