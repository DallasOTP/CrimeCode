using System.Security.Claims;
using CrimeCode.Data;
using CrimeCode.Models;
using Microsoft.EntityFrameworkCore;

namespace CrimeCode.Endpoints;

public static class AnalyticsEndpoints
{
    public static void MapAnalyticsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/analytics").RequireAuthorization();

        // Admin analytics dashboard
        group.MapGet("/dashboard", async (ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            var role = principal.FindFirstValue(ClaimTypes.Role);
            if (role != "Admin") return Results.Forbid();

            var now = DateTime.UtcNow;
            var last30 = now.AddDays(-30);
            var last7 = now.AddDays(-7);
            var today = now.Date;

            // Totals
            var totalUsers = await db.Users.CountAsync();
            var totalThreads = await db.Threads.CountAsync();
            var totalPosts = await db.Posts.CountAsync();
            var totalOrders = await db.MarketplaceOrders.CountAsync();
            var totalRevenue = await db.MarketplaceOrders.Where(o => o.Status == "Completed").SumAsync(o => o.Amount);

            // Today
            var usersToday = await db.Users.CountAsync(u => u.CreatedAt >= today);
            var postsToday = await db.Posts.CountAsync(p => p.CreatedAt >= today);
            var ordersToday = await db.MarketplaceOrders.CountAsync(o => o.CreatedAt >= today);

            // Last 7 days
            var usersWeek = await db.Users.CountAsync(u => u.CreatedAt >= last7);
            var postsWeek = await db.Posts.CountAsync(p => p.CreatedAt >= last7);
            var ordersWeek = await db.MarketplaceOrders.CountAsync(o => o.CreatedAt >= last7);

            // Daily data (last 30 days)
            var dailyUsers = await db.Users
                .Where(u => u.CreatedAt >= last30)
                .GroupBy(u => u.CreatedAt.Date)
                .Select(g => new DailyDataPoint(g.Key.ToString("yyyy-MM-dd"), g.Count()))
                .OrderBy(d => d.Date)
                .ToListAsync();

            var dailyPosts = await db.Posts
                .Where(p => p.CreatedAt >= last30)
                .GroupBy(p => p.CreatedAt.Date)
                .Select(g => new DailyDataPoint(g.Key.ToString("yyyy-MM-dd"), g.Count()))
                .OrderBy(d => d.Date)
                .ToListAsync();

            var dailyOrders = await db.MarketplaceOrders
                .Where(o => o.CreatedAt >= last30)
                .GroupBy(o => o.CreatedAt.Date)
                .Select(g => new DailyDataPoint(g.Key.ToString("yyyy-MM-dd"), g.Count()))
                .OrderBy(d => d.Date)
                .ToListAsync();

            // Top vendors
            var topVendors = await db.MarketplaceOrders
                .Where(o => o.Status == "Completed")
                .GroupBy(o => o.SellerId)
                .Select(g => new { SellerId = g.Key, Sales = g.Count(), Revenue = g.Sum(o => o.Amount) })
                .OrderByDescending(v => v.Sales)
                .Take(5)
                .ToListAsync();
            
            var vendorIds = topVendors.Select(v => v.SellerId).ToList();
            var vendorNames = await db.Users.Where(u => vendorIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.Username);

            var topVendorDtos = topVendors.Select(v => new TopVendorDto(
                v.SellerId, vendorNames.GetValueOrDefault(v.SellerId, "?"), v.Sales, v.Revenue
            )).ToList();

            // Active tickets
            var openTickets = await db.SupportTickets.CountAsync(t => t.Status == "Open" || t.Status == "InProgress");

            return Results.Ok(new AnalyticsDashboardDto(
                totalUsers, totalThreads, totalPosts, totalOrders, totalRevenue,
                usersToday, postsToday, ordersToday,
                usersWeek, postsWeek, ordersWeek,
                dailyUsers, dailyPosts, dailyOrders,
                topVendorDtos, openTickets
            ));
        });

        // Admin activity logs
        group.MapGet("/logs", async (int page, int pageSize, ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            var role = principal.FindFirstValue(ClaimTypes.Role);
            if (role != "Admin") return Results.Forbid();

            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 50;

            var total = await db.AdminLogs.CountAsync();
            var logs = await db.AdminLogs
                .Include(l => l.Admin)
                .OrderByDescending(l => l.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(l => new AdminLogDto(l.Id, l.Action, l.Details, l.TargetType, l.TargetId, l.CreatedAt, l.Admin.Username))
                .ToListAsync();

            return Results.Ok(new { logs, total, page, pageSize });
        });
    }

    // Helper to log admin actions from other endpoints
    public static async Task LogAction(CrimeCodeDbContext db, int adminId, string action, string details, string? targetType = null, int? targetId = null)
    {
        db.AdminLogs.Add(new AdminLog
        {
            AdminId = adminId,
            Action = action,
            Details = details,
            TargetType = targetType,
            TargetId = targetId
        });
        await db.SaveChangesAsync();
    }
}

public record DailyDataPoint(string Date, int Count);
public record TopVendorDto(int SellerId, string SellerName, int Sales, decimal Revenue);
public record AnalyticsDashboardDto(
    int TotalUsers, int TotalThreads, int TotalPosts, int TotalOrders, decimal TotalRevenue,
    int UsersToday, int PostsToday, int OrdersToday,
    int UsersWeek, int PostsWeek, int OrdersWeek,
    List<DailyDataPoint> DailyUsers, List<DailyDataPoint> DailyPosts, List<DailyDataPoint> DailyOrders,
    List<TopVendorDto> TopVendors, int OpenTickets
);
public record AdminLogDto(int Id, string Action, string Details, string? TargetType, int? TargetId, DateTime CreatedAt, string AdminName);
