using System.Collections.Concurrent;
using System.Net;

namespace CrimeCode.Middleware;

/// <summary>
/// IP-based DDoS protection: tracks request rates per IP, auto-bans IPs that exceed
/// burst thresholds, returns 429 for throttled IPs and 403 for banned IPs.
/// </summary>
public class DdosProtectionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<DdosProtectionMiddleware> _logger;

    // Sliding window tracking per IP
    private static readonly ConcurrentDictionary<string, IpTracker> _trackers = new();
    // Banned IPs with expiry
    private static readonly ConcurrentDictionary<string, DateTime> _bannedIps = new();

    // Thresholds
    private const int MaxRequestsPerSecond = 30;       // burst: 30 req/s
    private const int MaxRequestsPer10Seconds = 150;    // sustained: 15 req/s avg
    private const int MaxRequestsPerMinute = 600;       // long window
    private const int BanThresholdViolations = 3;       // violations before ban
    private const int BanDurationMinutes = 15;          // ban duration
    private const int CleanupIntervalSeconds = 60;

    private static DateTime _lastCleanup = DateTime.UtcNow;

    public DdosProtectionMiddleware(RequestDelegate next, ILogger<DdosProtectionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        // Skip healthcheck
        if (ctx.Request.Path.Equals("/healthz", StringComparison.OrdinalIgnoreCase))
        {
            await _next(ctx);
            return;
        }

        var ip = GetClientIp(ctx);

        // Periodic cleanup of expired entries
        if ((DateTime.UtcNow - _lastCleanup).TotalSeconds > CleanupIntervalSeconds)
        {
            _lastCleanup = DateTime.UtcNow;
            CleanupExpired();
        }

        // Check if IP is banned
        if (_bannedIps.TryGetValue(ip, out var banExpiry))
        {
            if (DateTime.UtcNow < banExpiry)
            {
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                ctx.Response.Headers["Retry-After"] = ((int)(banExpiry - DateTime.UtcNow).TotalSeconds).ToString();
                await ctx.Response.WriteAsJsonAsync(new { error = "IP temporarily banned due to abuse", retryAfter = (int)(banExpiry - DateTime.UtcNow).TotalSeconds });
                return;
            }
            _bannedIps.TryRemove(ip, out _);
        }

        var tracker = _trackers.GetOrAdd(ip, _ => new IpTracker());
        var now = DateTime.UtcNow;

        tracker.RecordRequest(now);

        var countLastSecond = tracker.GetCount(now, TimeSpan.FromSeconds(1));
        var countLast10Seconds = tracker.GetCount(now, TimeSpan.FromSeconds(10));
        var countLastMinute = tracker.GetCount(now, TimeSpan.FromMinutes(1));

        bool violated = countLastSecond > MaxRequestsPerSecond
                     || countLast10Seconds > MaxRequestsPer10Seconds
                     || countLastMinute > MaxRequestsPerMinute;

        if (violated)
        {
            tracker.Violations++;
            _logger.LogWarning("DDoS: IP {Ip} rate limit violated ({V} violations). 1s={C1}, 10s={C10}, 60s={C60}",
                ip, tracker.Violations, countLastSecond, countLast10Seconds, countLastMinute);

            if (tracker.Violations >= BanThresholdViolations)
            {
                var expiry = DateTime.UtcNow.AddMinutes(BanDurationMinutes);
                _bannedIps[ip] = expiry;
                _logger.LogWarning("DDoS: IP {Ip} BANNED for {Min} minutes", ip, BanDurationMinutes);

                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                await ctx.Response.WriteAsJsonAsync(new { error = "IP banned due to repeated abuse", retryAfter = BanDurationMinutes * 60 });
                return;
            }

            ctx.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            ctx.Response.Headers["Retry-After"] = "5";
            await ctx.Response.WriteAsJsonAsync(new { error = "Too many requests, slow down", retryAfter = 5 });
            return;
        }

        await _next(ctx);
    }

    private static string GetClientIp(HttpContext ctx)
    {
        // Check forwarded headers (Railway, reverse proxies)
        var forwarded = ctx.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwarded))
        {
            // Take the first (leftmost) IP — the original client
            var firstIp = forwarded.Split(',', StringSplitOptions.TrimEntries)[0];
            if (IPAddress.TryParse(firstIp, out _))
                return firstIp;
        }

        return ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private static void CleanupExpired()
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-2);

        foreach (var kvp in _bannedIps)
        {
            if (kvp.Value < DateTime.UtcNow)
                _bannedIps.TryRemove(kvp.Key, out _);
        }

        foreach (var kvp in _trackers)
        {
            if (kvp.Value.LastRequest < cutoff)
                _trackers.TryRemove(kvp.Key, out _);
        }
    }

    private class IpTracker
    {
        private readonly List<DateTime> _timestamps = new();
        private readonly object _lock = new();
        public int Violations { get; set; }
        public DateTime LastRequest { get; private set; }

        public void RecordRequest(DateTime now)
        {
            lock (_lock)
            {
                LastRequest = now;
                _timestamps.Add(now);

                // Keep only last 2 minutes of data
                var cutoff = now.AddMinutes(-2);
                _timestamps.RemoveAll(t => t < cutoff);
            }
        }

        public int GetCount(DateTime now, TimeSpan window)
        {
            var cutoff = now - window;
            lock (_lock)
            {
                return _timestamps.Count(t => t >= cutoff);
            }
        }
    }
}
