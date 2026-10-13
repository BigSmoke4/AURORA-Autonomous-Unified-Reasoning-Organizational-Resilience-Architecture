using System.Diagnostics;

namespace Aurora.Web.Infrastructure;

/// <summary>
/// Applies secure-by-default response headers (spec section 44) and exposes a correlation id on
/// every response so a user-reported problem can be traced through logs and spans (spec section 68).
/// The Content-Security-Policy forbids inline scripts and styles: all application JavaScript and CSS
/// lives under wwwroot.
/// </summary>
public sealed class SecurityHeadersMiddleware
{
    public const string CorrelationHeader = "X-Correlation-Id";

    private const string ContentSecurityPolicy =
        "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; font-src 'self'; " +
        "connect-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'; object-src 'none'";

    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next) => _next = next;

    public Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
        headers["Content-Security-Policy"] = ContentSecurityPolicy;
        headers[CorrelationHeader] = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
        return _next(context);
    }
}
