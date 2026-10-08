using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace ExpenseHub.Api.Endpoints;

internal sealed class ValidationFilter<T> : IEndpointFilter
    where T : class
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        T? request = context.Arguments.OfType<T>().FirstOrDefault();
        if (request is null)
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Corpo da requisição obrigatório.");
        }

        List<ValidationResult> results = [];
        if (!Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true))
        {
            Dictionary<string, string[]> errors = results
                .SelectMany(r => r.MemberNames.Select(m => (Member: m, Message: r.ErrorMessage ?? "Valor inválido.")))
                .GroupBy(e => e.Member)
                .ToDictionary(g => g.Key, g => g.Select(e => e.Message).ToArray());
            return Results.ValidationProblem(errors);
        }

        return await next(context);
    }
}
