using System.Security.Claims;
using CrimeCode.Data;
using CrimeCode.DTOs;
using Microsoft.EntityFrameworkCore;

namespace CrimeCode.Endpoints;

public static class VendorStatsEndpoints
{
    public static void MapVendorStatsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/vendor-stats").RequireAuthorization();

        // Get my vendor stats
        group.MapGet("/", async (ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            var userId = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var user = await db.Users.FindAsync(userId);
            if (user is null || !user.IsVendor) return Results.Forbid();

            var orders = await db.MarketplaceOrders.Where(o => o.SellerId == userId).ToListAsync();
            var completedOrders = orders.Where(o => o.Status == "Completed").ToList();

            var reviews = await db.VendorReviews.Where(r => r.SellerId == userId).ToListAsync();
            var avgRating = reviews.Count > 0 ? reviews.Average(r => r.Rating) : 0;

            var activeListings = await db.MarketplaceListings.CountAsync(l => l.SellerId == userId && l.Status == "Active");
            var pendingOrders = orders.Count(o => o.Status == "Created" || o.Status == "EscrowFunded");
            var disputedOrders = orders.Count(o => o.Status == "Disputed");

            var now = DateTime.UtcNow;
            var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var monthlyCompleted = completedOrders.Where(o => o.UpdatedAt >= monthStart).ToList();

            var last6 = new List<MonthlyStat>();
            for (int i = 5; i >= 0; i--)
            {
                var start = monthStart.AddMonths(-i);
                var end = start.AddMonths(1);
                var mOrders = completedOrders.Where(o => o.UpdatedAt >= start && o.UpdatedAt < end).ToList();
                last6.Add(new MonthlyStat(start.ToString("MMM yyyy"), mOrders.Count, mOrders.Sum(o => o.Amount)));
            }

            return Results.Ok(new VendorStatsDto(
                completedOrders.Count,
                completedOrders.Sum(o => o.Amount),
                activeListings,
                pendingOrders,
                disputedOrders,
                Math.Round(avgRating, 1),
                reviews.Count,
                monthlyCompleted.Sum(o => o.Amount),
                monthlyCompleted.Count,
                last6
            ));
        });

        // Public vendor stats
        group.MapGet("/{sellerId:int}", async (int sellerId, CrimeCodeDbContext db) =>
        {
            var user = await db.Users.FindAsync(sellerId);
            if (user is null || !user.IsVendor) return Results.NotFound();

            var completed = await db.MarketplaceOrders.CountAsync(o => o.SellerId == sellerId && o.Status == "Completed");
            var revenue = await db.MarketplaceOrders.Where(o => o.SellerId == sellerId && o.Status == "Completed").SumAsync(o => o.Amount);
            var activeListings = await db.MarketplaceListings.CountAsync(l => l.SellerId == sellerId && l.Status == "Active");
            var reviews = await db.VendorReviews.Where(r => r.SellerId == sellerId).ToListAsync();
            var avgRating = reviews.Count > 0 ? reviews.Average(r => r.Rating) : 0;

            return Results.Ok(new
            {
                sellerId,
                username = user.Username,
                totalSales = completed,
                totalRevenue = revenue,
                activeListings,
                averageRating = Math.Round(avgRating, 1),
                totalReviews = reviews.Count,
                vendorSince = user.VendorApprovedAt
            });
        }).AllowAnonymous();
    }
}
