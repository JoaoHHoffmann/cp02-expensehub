using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Domain;
using ExpenseHub.Api.Endpoints;
using ExpenseHub.Api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests;

/// <summary>Verifica as políticas reais das rotas e as respostas de erro sem servidor ou banco.</summary>
[TestClass]
public sealed class ExpenseHttpContractTests
{
    private const string TestScheme = "UnitTest";

    /// <summary>Todas as rotas de reembolsos respondem 401 para uma identidade anônima.</summary>
    [TestMethod]
    public async Task ExpenseRoutes_AnonymousUser_IsUnauthorized()
    {
        await using WebApplication app = CreateApplication();
        foreach (RouteEndpoint endpoint in GetExpenseEndpoints(app))
        {
            int status = await AuthorizeAsync(app, endpoint, new ClaimsPrincipal(new ClaimsIdentity()));

            Assert.AreEqual(StatusCodes.Status401Unauthorized, status, endpoint.DisplayName);
        }
    }

    /// <summary>Todas as rotas recusam uma identidade autenticada sem roles funcionais.</summary>
    [TestMethod]
    public async Task ExpenseRoutes_AuthenticatedUserWithoutRoles_IsForbidden()
    {
        await using WebApplication app = CreateApplication();
        foreach (RouteEndpoint endpoint in GetExpenseEndpoints(app))
        {
            int status = await AuthorizeAsync(app, endpoint, ExpenseTestData.CreateUser());

            Assert.AreEqual(StatusCodes.Status403Forbidden, status, endpoint.DisplayName);
        }
    }

    /// <summary>As políticas das rotas implementam as permissões funcionais de cada role.</summary>
    /// <param name="role">Role da identidade autenticada.</param>
    [TestMethod]
    [DataRow(Roles.Employee)]
    [DataRow(Roles.Approver)]
    [DataRow(Roles.Finance)]
    [DataRow(Roles.Auditor)]
    [DataRow(Roles.Admin)]
    [DataRow("Unknown")]
    public async Task ExpenseRoutes_SingleRole_RespectsAccessMatrix(string role)
    {
        await using WebApplication app = CreateApplication();
        foreach (RouteEndpoint endpoint in GetExpenseEndpoints(app))
        {
            int expected = CanAccessRoute(endpoint, role)
                ? StatusCodes.Status204NoContent
                : StatusCodes.Status403Forbidden;

            int status = await AuthorizeAsync(app, endpoint, ExpenseTestData.CreateUser(role));

            Assert.AreEqual(expected, status, $"{role}: {endpoint.DisplayName}");
        }
    }

    /// <summary>Uma role adicional acumula acesso sem tornar Admin uma permissão funcional.</summary>
    /// <param name="firstRole">Primeira role da identidade.</param>
    /// <param name="secondRole">Segunda role da identidade.</param>
    [TestMethod]
    [DataRow(Roles.Employee, Roles.Approver)]
    [DataRow(Roles.Admin, Roles.Employee)]
    [DataRow(Roles.Admin, Roles.Finance)]
    [DataRow(Roles.Auditor, Roles.Approver)]
    public async Task ExpenseRoutes_MultipleRoles_AccumulatePermissions(string firstRole, string secondRole)
    {
        await using WebApplication app = CreateApplication();
        foreach (RouteEndpoint endpoint in GetExpenseEndpoints(app))
        {
            int expected = CanAccessRoute(endpoint, firstRole) || CanAccessRoute(endpoint, secondRole)
                ? StatusCodes.Status204NoContent
                : StatusCodes.Status403Forbidden;

            int status = await AuthorizeAsync(app, endpoint, ExpenseTestData.CreateUser(firstRole, secondRole));

            Assert.AreEqual(expected, status, $"{firstRole} + {secondRole}: {endpoint.DisplayName}");
        }
    }

    /// <summary>As falhas do domínio retornam o status HTTP correspondente em ProblemDetails.</summary>
    /// <param name="errorType">Tipo de falha do domínio.</param>
    /// <param name="expectedStatus">Status HTTP esperado.</param>
    [TestMethod]
    [DataRow((int)DomainErrorType.Validation, StatusCodes.Status400BadRequest)]
    [DataRow((int)DomainErrorType.Forbidden, StatusCodes.Status403Forbidden)]
    [DataRow((int)DomainErrorType.NotFound, StatusCodes.Status404NotFound)]
    [DataRow((int)DomainErrorType.Conflict, StatusCodes.Status409Conflict)]
    public async Task ExceptionHandler_DomainError_WritesExpectedProblemDetails(int errorType, int expectedStatus)
    {
        DomainException exception = new((DomainErrorType)errorType, "Falha esperada da regra de negócio.");

        string title = await HandleExceptionAsync(exception, expectedStatus);

        Assert.AreEqual(exception.Message, title);
    }

    /// <summary>Um conflito de concorrência retorna 409 com uma mensagem segura.</summary>
    [TestMethod]
    public async Task ExceptionHandler_ConcurrentUpdate_IsConflict()
    {
        DbUpdateConcurrencyException exception = new("Detalhes internos da persistência.");

        string title = await HandleExceptionAsync(exception, StatusCodes.Status409Conflict);

        Assert.AreNotEqual(exception.Message, title);
        StringAssert.Contains(title, "reembolso");
    }

    /// <summary>Uma requisição malformada preserva o status definido pelo ASP.NET Core.</summary>
    [TestMethod]
    public async Task ExceptionHandler_BadHttpRequest_PreservesStatus()
    {
        BadHttpRequestException exception = new("Corpo da requisição inválido.", StatusCodes.Status400BadRequest);

        string title = await HandleExceptionAsync(exception, StatusCodes.Status400BadRequest);

        Assert.AreEqual(exception.Message, title);
    }

    /// <summary>Erros inesperados ficam disponíveis para o tratamento padrão de falhas internas.</summary>
    [TestMethod]
    public async Task ExceptionHandler_UnexpectedError_DoesNotWriteResponse()
    {
        using ServiceProvider services = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        using MemoryStream body = new();
        DefaultHttpContext context = new() { RequestServices = services };
        context.Response.Body = body;
        ApiExceptionHandler handler = new(services.GetRequiredService<IProblemDetailsService>());

        bool handled = await handler.TryHandleAsync(context, new InvalidOperationException("Falha inesperada."), CancellationToken.None);

        Assert.IsFalse(handled);
        Assert.AreEqual(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.AreEqual(0L, body.Length);
    }

    private static WebApplication CreateApplication()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = [],
            ContentRootPath = AppContext.BaseDirectory,
        });
        builder.Services.AddAuthorization();
        builder.Services.AddAuthentication(TestScheme)
            .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(TestScheme, _ => { });
        // O registro permite inferir os parâmetros de DI; o serviço nunca é instanciado.
        builder.Services.AddScoped<ExpenseService>(_ => throw new InvalidOperationException("O serviço não deve ser executado neste teste de autorização."));
        WebApplication app = builder.Build();
        app.MapExpenseEndpoints();
        return app;
    }

    private static RouteEndpoint[] GetExpenseEndpoints(WebApplication app)
    {
        RouteEndpoint[] endpoints = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .ToArray();
        Assert.IsGreaterThanOrEqualTo(5, endpoints.Length, "As rotas existentes de reembolsos devem estar mapeadas.");
        return endpoints;
    }

    private static bool CanAccessRoute(RouteEndpoint endpoint, string role)
    {
        IHttpMethodMetadata? methods = endpoint.Metadata.GetMetadata<IHttpMethodMetadata>();
        Assert.IsNotNull(methods);
        if (methods.HttpMethods.Contains(HttpMethods.Get))
        {
            return role is Roles.Employee or Roles.Approver or Roles.Finance or Roles.Auditor;
        }

        string path = endpoint.RoutePattern.RawText ?? string.Empty;
        if (path.EndsWith("/approve", StringComparison.Ordinal) || path.EndsWith("/reject", StringComparison.Ordinal))
        {
            return role == Roles.Approver;
        }

        return role == Roles.Employee;
    }

    private static async Task<int> AuthorizeAsync(WebApplication app, RouteEndpoint endpoint, ClaimsPrincipal user)
    {
        using IServiceScope scope = app.Services.CreateScope();
        AuthorizationPolicy? policy = await AuthorizationPolicy.CombineAsync(
            scope.ServiceProvider.GetRequiredService<IAuthorizationPolicyProvider>(),
            endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>(),
            endpoint.Metadata.GetOrderedMetadata<AuthorizationPolicy>());
        Assert.IsNotNull(policy, "Cada rota de reembolsos deve conter uma política de autorização.");
        DefaultHttpContext context = new() { RequestServices = scope.ServiceProvider, User = user };
        context.SetEndpoint(endpoint);
        AuthenticateResult authentication = user.Identity?.IsAuthenticated == true
            ? AuthenticateResult.Success(new AuthenticationTicket(user, TestScheme))
            : AuthenticateResult.NoResult();
        PolicyAuthorizationResult result = await scope.ServiceProvider.GetRequiredService<IPolicyEvaluator>()
            .AuthorizeAsync(policy, authentication, context, endpoint);

        // 204 marca que a autorização permitiu continuar; o endpoint não é executado.
        await new AuthorizationMiddlewareResultHandler().HandleAsync(
            httpContext =>
            {
                httpContext.Response.StatusCode = StatusCodes.Status204NoContent;
                return Task.CompletedTask;
            }, context, policy, result);
        return context.Response.StatusCode;
    }

    private static async Task<string> HandleExceptionAsync(Exception exception, int expectedStatus)
    {
        using ServiceProvider services = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        using MemoryStream body = new();
        DefaultHttpContext context = new() { RequestServices = services };
        context.Request.Headers.Accept = "application/problem+json";
        context.Response.Body = body;
        ApiExceptionHandler handler = new(services.GetRequiredService<IProblemDetailsService>());

        bool handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        Assert.IsTrue(handled);
        Assert.AreEqual(expectedStatus, context.Response.StatusCode);
        Assert.IsNotNull(context.Response.ContentType);
        StringAssert.Contains(context.Response.ContentType, "application/problem+json");
        using JsonDocument document = JsonDocument.Parse(body.ToArray());
        Assert.AreEqual(expectedStatus, document.RootElement.GetProperty("status").GetInt32());
        string? title = document.RootElement.GetProperty("title").GetString();
        Assert.IsNotNull(title);
        return title;
    }

    private sealed class TestAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public TestAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync() => Task.FromResult(AuthenticateResult.NoResult());
    }
}
