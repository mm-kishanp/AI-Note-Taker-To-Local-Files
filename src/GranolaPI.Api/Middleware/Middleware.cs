using System.Diagnostics;
using System.Net;
using GranolaPI.Application.Export;
using GranolaPI.Application.Granola;
using GranolaPI.Application.Meetings;
using GranolaPI.Application.Synchronization;
using GranolaPI.Domain.Enums;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace GranolaPI.Api.Middleware;

/// <summary>Maps application exceptions to RFC 7807 problem details. Never returns stack traces or secrets.</summary>
public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger, IHostEnvironment env) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception ex, CancellationToken ct)
    {
        var (status, title, category) = ex switch
        {
            ValidationException => (StatusCodes.Status400BadRequest, "Validation failed", ErrorCategory.Configuration),
            NotFoundException => (StatusCodes.Status404NotFound, "Not found", ErrorCategory.NotFound),
            SyncAlreadyRunningException => (StatusCodes.Status409Conflict, "Synchronization already running", ErrorCategory.SyncConflict),
            GranolaApiException g when g.Category is ErrorCategory.Authentication or ErrorCategory.Permission => (StatusCodes.Status424FailedDependency, "Granola rejected the request", g.Category),
            GranolaApiException g when g.Category == ErrorCategory.Configuration => (StatusCodes.Status400BadRequest, "Granola is not configured", g.Category),
            GranolaApiException g => (StatusCodes.Status502BadGateway, "Granola API error", g.Category),
            ExportFileException f => (StatusCodes.Status500InternalServerError, "File system error", f.Category),
            OperationCanceledException when http.RequestAborted.IsCancellationRequested => (499, "Request cancelled", ErrorCategory.Cancelled),
            _ => (StatusCodes.Status500InternalServerError, "Unexpected error", ErrorCategory.Unknown),
        };

        if (status >= 500) logger.LogError(ex, "Request {Path} failed", http.Request.Path);
        else logger.LogInformation("Request {Path} returned {Status}: {Message}", http.Request.Path, status, ex.Message);

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = status == 500 && category == ErrorCategory.Unknown && !env.IsDevelopment() ? "An unexpected error occurred. See the application log." : ex.Message,
            Instance = http.Request.Path,
        };
        problem.Extensions["category"] = category.ToString();
        problem.Extensions["correlationId"] = http.TraceIdentifier;
        if (ex is ValidationException v)
            problem.Extensions["errors"] = v.Errors.Select(e => new { e.Field, e.Message }).ToList();

        http.Response.StatusCode = status;
        await http.Response.WriteAsJsonAsync(problem, ct);
        return true;
    }
}

/// <summary>
/// Request protection for a local-first app:
///  - Correlation id on every request/response (X-Correlation-ID).
///  - State-changing /api calls must carry "X-GPI-Request: 1". Browsers cannot add custom headers
///    cross-site without a CORS preflight, which is refused for unknown origins (CSRF protection).
///  - Non-loopback callers are rejected unless an API token is configured (Security:ApiToken),
///    in which case every /api call except /api/health must present it as a bearer token.
/// </summary>
public sealed class RequestProtectionMiddleware(RequestDelegate next, IConfiguration config)
{
    public const string CsrfHeader = "X-GPI-Request";

    public async Task InvokeAsync(HttpContext http)
    {
        var correlation = http.Request.Headers["X-Correlation-ID"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(correlation) || correlation.Length > 64) correlation = Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
        http.TraceIdentifier = correlation;
        http.Response.Headers["X-Correlation-ID"] = correlation;
        http.Response.Headers["X-Content-Type-Options"] = "nosniff";
        http.Response.Headers["Referrer-Policy"] = "no-referrer";

        var path = http.Request.Path;
        if (path.StartsWithSegments("/api") && !path.StartsWithSegments("/api/health"))
        {
            var token = config["Security:ApiToken"];
            var remote = http.Connection.RemoteIpAddress;
            var isLoopback = remote is null || IPAddress.IsLoopback(remote);
            if (!string.IsNullOrEmpty(token))
            {
                var auth = http.Request.Headers.Authorization.FirstOrDefault();
                if (auth is null || !System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                        System.Text.Encoding.UTF8.GetBytes(auth), System.Text.Encoding.UTF8.GetBytes("Bearer " + token)))
                {
                    await Reject(http, StatusCodes.Status401Unauthorized, "A valid API token is required.");
                    return;
                }
            }
            else if (!isLoopback && !config.GetValue("Security:AllowRemoteWithoutToken", false))
            {
                await Reject(http, StatusCodes.Status403Forbidden, "Remote access is disabled. Configure Security:ApiToken to allow it.");
                return;
            }

            if (!HttpMethods.IsGet(http.Request.Method) && !HttpMethods.IsHead(http.Request.Method) && !HttpMethods.IsOptions(http.Request.Method)
                && http.Request.Headers[CsrfHeader] != "1")
            {
                await Reject(http, StatusCodes.Status403Forbidden, $"Missing {CsrfHeader} header.");
                return;
            }
        }
        await next(http);
    }

    private static Task Reject(HttpContext http, int status, string message)
    {
        http.Response.StatusCode = status;
        return http.Response.WriteAsJsonAsync(new ProblemDetails { Status = status, Title = message, Instance = http.Request.Path });
    }
}
