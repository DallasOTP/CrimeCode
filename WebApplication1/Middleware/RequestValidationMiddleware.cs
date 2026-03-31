using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace CrimeCode.Middleware;

/// <summary>
/// Request validation middleware: blocks suspicious payloads, oversized bodies,
/// and common attack patterns (SQL injection, path traversal, script injection).
/// </summary>
public partial class RequestValidationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestValidationMiddleware> _logger;

    private const long MaxBodySize = 5 * 1024 * 1024; // 5 MB max body

    public RequestValidationMiddleware(RequestDelegate next, ILogger<RequestValidationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        var path = ctx.Request.Path.Value ?? "";
        var query = ctx.Request.QueryString.Value ?? "";

        // Block path traversal attempts
        if (path.Contains("..") || path.Contains("//") || path.Contains("\\"))
        {
            _logger.LogWarning("Blocked path traversal attempt: {Path} from {Ip}", path, GetIp(ctx));
            ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
            await ctx.Response.WriteAsJsonAsync(new { error = "Invalid request path" });
            return;
        }

        // Block common injection patterns in query string
        if (HasInjectionPattern(query))
        {
            _logger.LogWarning("Blocked injection attempt in query: {Query} from {Ip}", query, GetIp(ctx));
            ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
            await ctx.Response.WriteAsJsonAsync(new { error = "Invalid request" });
            return;
        }

        // Block oversized request bodies
        if (ctx.Request.ContentLength > MaxBodySize)
        {
            ctx.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            await ctx.Response.WriteAsJsonAsync(new { error = "Request body too large" });
            return;
        }

        // Block suspicious User-Agent patterns (common bot/tool signatures)
        var ua = ctx.Request.Headers.UserAgent.ToString();
        if (IsSuspiciousUserAgent(ua))
        {
            _logger.LogWarning("Blocked suspicious User-Agent: {UA} from {Ip}", ua, GetIp(ctx));
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            await ctx.Response.WriteAsJsonAsync(new { error = "Access denied" });
            return;
        }

        await _next(ctx);
    }

    private static bool HasInjectionPattern(string input)
    {
        if (string.IsNullOrEmpty(input)) return false;
        var lower = input.ToLowerInvariant();

        // SQL injection patterns
        if (lower.Contains("union select") || lower.Contains("' or '") || lower.Contains("1=1")
            || lower.Contains("drop table") || lower.Contains("insert into")
            || lower.Contains("delete from") || lower.Contains("update set")
            || lower.Contains("exec(") || lower.Contains("execute(")
            || lower.Contains("xp_cmdshell") || lower.Contains("sp_executesql"))
            return true;

        // Script injection in query
        if (lower.Contains("<script") || lower.Contains("javascript:") || lower.Contains("onerror=")
            || lower.Contains("onload=") || lower.Contains("eval("))
            return true;

        return false;
    }

    private static bool IsSuspiciousUserAgent(string ua)
    {
        if (string.IsNullOrEmpty(ua)) return false;
        var lower = ua.ToLowerInvariant();

        // Known attack tools / scanners
        return lower.Contains("sqlmap") || lower.Contains("nikto") || lower.Contains("nmap")
            || lower.Contains("masscan") || lower.Contains("zgrab") || lower.Contains("gobuster")
            || lower.Contains("dirbuster") || lower.Contains("wfuzz") || lower.Contains("hydra")
            || lower.Contains("metasploit");
    }

    private static string GetIp(HttpContext ctx)
    {
        var forwarded = ctx.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwarded))
            return forwarded.Split(',', StringSplitOptions.TrimEntries)[0];
        return ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
