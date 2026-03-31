using System.Security.Claims;
using CrimeCode.Data;
using CrimeCode.DTOs;
using Microsoft.EntityFrameworkCore;

namespace CrimeCode.Endpoints;

public static class LeaderboardEndpoints
{
    public static void MapLeaderboardEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/leaderboard");

        // Top members by reputation
        group.MapGet("/reputation", async (CrimeCodeDbContext db) =>
        {
            var ranks = await db.UserRanks.OrderByDescending(r => r.MinPosts).ToListAsync();

            var users = await db.Users
                .Include(u => u.Posts)
                .Include(u => u.Threads)
                .OrderByDescending(u => u.ReputationScore)
                .Take(25)
                .ToListAsync();

            var result = users.Select(u =>
            {
                var rank = GetRank(u, ranks);
                return new LeaderboardEntry(u.Id, u.Username, u.AvatarUrl, u.Role,
                    rank.Name, rank.Color, rank.Icon,
                    u.Posts.Count, u.Threads.Count, u.ReputationScore, u.Credits);
            }).ToList();

            return Results.Ok(result);
        });

        // Top members by posts
        group.MapGet("/posts", async (CrimeCodeDbContext db) =>
        {
            var ranks = await db.UserRanks.OrderByDescending(r => r.MinPosts).ToListAsync();

            var users = await db.Users
                .Include(u => u.Posts)
                .Include(u => u.Threads)
                .OrderByDescending(u => u.Posts.Count)
                .Take(25)
                .ToListAsync();

            var result = users.Select(u =>
            {
                var rank = GetRank(u, ranks);
                return new LeaderboardEntry(u.Id, u.Username, u.AvatarUrl, u.Role,
                    rank.Name, rank.Color, rank.Icon,
                    u.Posts.Count, u.Threads.Count, u.ReputationScore, u.Credits);
            }).ToList();

            return Results.Ok(result);
        });

        // Top members by credits
        group.MapGet("/credits", async (CrimeCodeDbContext db) =>
        {
            var ranks = await db.UserRanks.OrderByDescending(r => r.MinPosts).ToListAsync();

            var users = await db.Users
                .Include(u => u.Posts)
                .Include(u => u.Threads)
                .OrderByDescending(u => u.Credits)
                .Take(25)
                .ToListAsync();

            var result = users.Select(u =>
            {
                var rank = GetRank(u, ranks);
                return new LeaderboardEntry(u.Id, u.Username, u.AvatarUrl, u.Role,
                    rank.Name, rank.Color, rank.Icon,
                    u.Posts.Count, u.Threads.Count, u.ReputationScore, u.Credits);
            }).ToList();

            return Results.Ok(result);
        });

        // Forum stats
        group.MapGet("/stats", async (CrimeCodeDbContext db) =>
        {
            var cutoff = DateTime.UtcNow.AddMinutes(-15);
            var onlineCount = await db.Users.CountAsync(u => u.LastSeenAt > cutoff);
            var newestMember = await db.Users.OrderByDescending(u => u.CreatedAt).Select(u => u.Username).FirstOrDefaultAsync();

            return Results.Ok(new ForumStatsDto(
                TotalUsers: await db.Users.CountAsync(),
                TotalThreads: await db.Threads.CountAsync(),
                TotalPosts: await db.Posts.CountAsync(),
                OnlineUsers: onlineCount,
                NewestMember: newestMember
            ));
        });

        // Online users list
        group.MapGet("/online", async (CrimeCodeDbContext db) =>
        {
            var cutoff = DateTime.UtcNow.AddMinutes(-15);
            var ranks = await db.UserRanks.OrderByDescending(r => r.MinPosts).ToListAsync();

            var users = await db.Users
                .Include(u => u.Posts)
                .Where(u => u.LastSeenAt > cutoff)
                .ToListAsync();

            var result = users.Select(u =>
            {
                var rank = GetRank(u, ranks);
                return new OnlineUserDto(u.Id, u.Username, u.AvatarUrl, u.Role, rank.Name, rank.Color, u.Status ?? "online");
            }).ToList();

            return Results.Ok(result);
        });

        // Thread tags
        group.MapGet("/tags", async (CrimeCodeDbContext db) =>
        {
            var tags = await db.ThreadTags
                .Select(t => new ThreadTagDto(t.Id, t.Name, t.Color))
                .ToListAsync();
            return Results.Ok(tags);
        });

        // User ranks
        group.MapGet("/ranks", async (CrimeCodeDbContext db) =>
        {
            var ranks = await db.UserRanks
                .OrderBy(r => r.SortOrder)
                .Select(r => new UserRankDto(r.Id, r.Name, r.Color, r.Icon, r.MinPosts, r.MinReputation))
                .ToListAsync();
            return Results.Ok(ranks);
        });
    }

    public static CrimeCode.Models.UserRank GetRank(CrimeCode.Models.User user, List<CrimeCode.Models.UserRank> ranks)
    {
        var postCount = user.Posts?.Count ?? 0;
        var rep = user.ReputationScore;

        foreach (var rank in ranks) // Already sorted by MinPosts desc
        {
            if (postCount >= rank.MinPosts && rep >= rank.MinReputation)
                return rank;
        }

        return ranks.LastOrDefault() ?? new CrimeCode.Models.UserRank { Name = "Newbie", Color = "#6b7fa0", Icon = "🔰" };
    }
}
