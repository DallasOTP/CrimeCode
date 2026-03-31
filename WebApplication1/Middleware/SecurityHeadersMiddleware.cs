using System.Collections.Concurrent;

namespace CrimeCode.Middleware;

/// <summary>
/// Adds security headers to every response: CSP, HSTS, X-Frame-Options, anti-sniffing,
/// Referrer-Policy, Permissions-Policy. Strips server identity headers.
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext ctx)
    {
        var h = ctx.Response.Headers;

        // Anti-clickjacking
        h["X-Frame-Options"] = "DENY";

        // Prevent MIME-type sniffing
        h["X-Content-Type-Options"] = "nosniff";

        // XSS reflection filter (legacy browsers)
        h["X-XSS-Protection"] = "1; mode=block";

        // Strict transport security (1 year, include subdomains)
        h["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";

        // Referrer — send origin only on cross-origin, full on same-origin
        h["Referrer-Policy"] = "strict-origin-when-cross-origin";

        // Permissions policy — disable sensors, geolocation, camera, microphone, payment
        h["Permissions-Policy"] = "geolocation=(), camera=(), microphone=(), payment=(), usb=(), magnetometer=(), gyroscope=(), accelerometer=()";

        // Content-Security-Policy — tight but allow placehold.co images and inline styles/scripts (SPA)
        h["Content-Security-Policy"] =
            "default-src 'self'; " +
            "script-src 'self' 'unsafe-inline'; " +
            "style-src 'self' 'unsafe-inline'; " +
            "img-src 'self' data: https://placehold.co https://*.placehold.co; " +
            "font-src 'self'; " +
            "connect-src 'self'; " +
            "frame-ancestors 'none'; " +
            "base-uri 'self'; " +
            "form-action 'self';";

        // Hide server identity
        h.Remove("Server");
        h.Remove("X-Powered-By");

        // Prevent caching of API responses (HTML/static files handled by UseStaticFiles)
        if (ctx.Request.Path.StartsWithSegments("/api"))
        {
            h["Cache-Control"] = "no-store, no-cache, must-revalidate";
            h["Pragma"] = "no-cache";
        }

        await _next(ctx);
    }
}
