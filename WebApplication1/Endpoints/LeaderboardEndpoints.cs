using CrimeCode.Data;
using CrimeCode.DTOs;
using Microsoft.EntityFrameworkCore;

namespace CrimeCode.Endpoints;

public static class LeaderboardEndpoints
{
    public static void MapLeaderboardEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/leaderboard");

        // Top vendors by rating
        group.MapGet("/rating", async (CrimeCodeDbContext db) =>
        {
            var vendors = await db.Users
                .Where(u => u.IsVendor)
                .Select(u => new
                {
                    u.Id, u.Username, u.AvatarUrl, u.VendorBio, u.ReputationScore,
                    TotalSales = u.SellerOrders.Count(o => o.Status == "Completed"),
                    TotalRevenue = u.SellerOrders.Where(o => o.Status == "Completed").Sum(o => o.Amount),
                    AvgRating = u.ReviewsReceived.Any() ? u.ReviewsReceived.Average(r => (double)r.Rating) : 0,
                    ReviewCount = u.ReviewsReceived.Count()
                })
                .OrderByDescending(v => v.AvgRating).ThenByDescending(v => v.ReviewCount)
                .Take(25)
                .ToListAsync();

            return Results.Ok(vendors.Select(v => new VendorLeaderboardEntry(
                v.Id, v.Username, v.AvatarUrl, v.VendorBio,
                v.TotalSales, Math.Round(v.AvgRating, 1), v.ReviewCount, v.ReputationScore, v.TotalRevenue)));
        });

        // Top vendors by sales count
        group.MapGet("/sales", async (CrimeCodeDbContext db) =>
        {
            var vendors = await db.Users
                .Where(u => u.IsVendor)
                .Select(u => new
                {
                    u.Id, u.Username, u.AvatarUrl, u.VendorBio, u.ReputationScore,
                    TotalSales = u.SellerOrders.Count(o => o.Status == "Completed"),
                    TotalRevenue = u.SellerOrders.Where(o => o.Status == "Completed").Sum(o => o.Amount),
                    AvgRating = u.ReviewsReceived.Any() ? u.ReviewsReceived.Average(r => (double)r.Rating) : 0,
                    ReviewCount = u.ReviewsReceived.Count()
                })
                .OrderByDescending(v => v.TotalSales)
                .Take(25)
                .ToListAsync();

            return Results.Ok(vendors.Select(v => new VendorLeaderboardEntry(
                v.Id, v.Username, v.AvatarUrl, v.VendorBio,
                v.TotalSales, Math.Round(v.AvgRating, 1), v.ReviewCount, v.ReputationScore, v.TotalRevenue)));
        });

        // Top vendors by reputation
        group.MapGet("/reputation", async (CrimeCodeDbContext db) =>
        {
            var vendors = await db.Users
                .Where(u => u.IsVendor)
                .Select(u => new
                {
                    u.Id, u.Username, u.AvatarUrl, u.VendorBio, u.ReputationScore,
                    TotalSales = u.SellerOrders.Count(o => o.Status == "Completed"),
                    TotalRevenue = u.SellerOrders.Where(o => o.Status == "Completed").Sum(o => o.Amount),
                    AvgRating = u.ReviewsReceived.Any() ? u.ReviewsReceived.Average(r => (double)r.Rating) : 0,
                    ReviewCount = u.ReviewsReceived.Count()
                })
                .OrderByDescending(v => v.ReputationScore)
                .Take(25)
                .ToListAsync();

            return Results.Ok(vendors.Select(v => new VendorLeaderboardEntry(
                v.Id, v.Username, v.AvatarUrl, v.VendorBio,
                v.TotalSales, Math.Round(v.AvgRating, 1), v.ReviewCount, v.ReputationScore, v.TotalRevenue)));
        });

        // Marketplace stats (replaces forum stats)
        group.MapGet("/stats", async (CrimeCodeDbContext db) =>
        {
            var cutoff = DateTime.UtcNow.AddMinutes(-15);
            var onlineCount = await db.Users.CountAsync(u => u.LastSeenAt > cutoff);
            var newestMember = await db.Users.OrderByDescending(u => u.CreatedAt).Select(u => u.Username).FirstOrDefaultAsync();

            return Results.Ok(new MarketplaceStatsDto(
                TotalUsers: await db.Users.CountAsync(),
                TotalListings: await db.MarketplaceListings.CountAsync(l => l.Status == "Active"),
                TotalSales: await db.MarketplaceOrders.CountAsync(o => o.Status == "Completed"),
                OnlineUsers: onlineCount,
                NewestMember: newestMember
            ));
        });

        // Online users list
        group.MapGet("/online", async (CrimeCodeDbContext db) =>
        {
            var cutoff = DateTime.UtcNow.AddMinutes(-15);
            var users = await db.Users
                .Where(u => u.LastSeenAt > cutoff)
                .Select(u => new OnlineUserDto(u.Id, u.Username, u.AvatarUrl, u.Role, u.Status ?? "online"))
                .ToListAsync();

            return Results.Ok(users);
        });
    }
}
