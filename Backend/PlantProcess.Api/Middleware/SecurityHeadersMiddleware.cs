using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace PlantProcess.Api.Middleware;

/// <summary>
/// P1-04: baseline HTTP security headers. HSTS is added separately via app.UseHsts().
/// The Content-Security-Policy can be overridden per environment via the
/// "Security:ContentSecurityPolicy" configuration key.
/// </summary>
public sealed class SecurityHeadersMiddleware
{
    private const string DefaultCsp =
        "default-src 'self'; img-src 'self' data:; style-src 'self' 'unsafe-inline'; " +
        "script-src 'self'; connect-src 'self'; font-src 'self' data:; " +
        "object-src 'none'; base-uri 'self'; frame-ancestors 'none'";

    private readonly RequestDelegate _next;
    private readonly string _csp;

    public SecurityHeadersMiddleware(RequestDelegate next, IConfiguration config)
    {
        _next = next;
        var configured = config["Security:ContentSecurityPolicy"];
        _csp = string.IsNullOrWhiteSpace(configured) ? DefaultCsp : configured;
    }

    public Task InvokeAsync(HttpContext ctx)
    {
        var h = ctx.Response.Headers;
        h["X-Content-Type-Options"] = "nosniff";
        h["X-Frame-Options"]        = "DENY";
        h["Referrer-Policy"]        = "no-referrer";
        if (!h.ContainsKey("Content-Security-Policy"))
            h["Content-Security-Policy"] = _csp;
        return _next(ctx);
    }
}
