using Microsoft.AspNetCore.Mvc;

namespace EmployeeManagement.Server.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            // Presim hapat pasardhes; pergjigjet normale, perfshire 400/401/403/404/409, nuk ndryshohen.
            await _next(context);
        }
        catch (Exception exception)
        {
            // Detajet e plota ruhen vetem ne log-un e serverit, bashke me ID-ne e kerkeses.
            _logger.LogError(exception, "Gabim gjate perpunimit te kerkeses. TraceId: {TraceId}", context.TraceIdentifier);

            // Pasi headers jane derguar, nuk mund te shkruajme nje pergjigje te dyte JSON.
            if (context.Response.HasStarted)
                throw;

            // Gabimet e protokollit HTTP ruajne statusin e tyre (p.sh. 400 ose 413).
            // Vetem gabimet e papritura kthehen si 500; mesazhi i exception nuk i dergohet klientit.
            var status = exception is BadHttpRequestException requestException
                ? requestException.StatusCode
                : StatusCodes.Status500InternalServerError;

            var problem = new ProblemDetails
            {
                Status = status,
                Title = status == StatusCodes.Status500InternalServerError
                    ? "An unexpected error occurred."
                    : "The HTTP request could not be processed.",
                Type = "about:blank"
            };
            // Klienti mund te raportoje kete ID per ta gjetur gabimin ne log, pa pare detaje sensitive.
            problem.Extensions["traceId"] = context.TraceIdentifier;

            context.Response.Clear();
            context.Response.StatusCode = status;
            await context.Response.WriteAsJsonAsync(problem, options: null,
                contentType: "application/problem+json", cancellationToken: context.RequestAborted);
        }
    }
}
