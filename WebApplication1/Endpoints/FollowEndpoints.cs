using System.Security.Claims;
using CrimeCode.Data;
using CrimeCode.DTOs;
using CrimeCode.Models;
using Microsoft.EntityFrameworkCore;

namespace CrimeCode.Endpoints;

public static class FollowEndpoints
{
    public static void MapFollowEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/users/{userId:int}");

        // Follow user
        group.MapPost("/follow", async (int userId, ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            var followerId = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            if (followerId == userId) return Results.BadRequest("Non puoi seguire te stesso.");

            var target = await db.Users.FindAsync(userId);
            if (target is null) return Results.NotFound();

            var existing = await db.UserFollows.FirstOrDefaultAsync(f => f.FollowerId == followerId && f.FollowingId == userId);
            if (existing is not null) return Results.Conflict("Già segui questo utente.");

            db.UserFollows.Add(new UserFollow
            {
                FollowerId = followerId,
                FollowingId = userId,
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();

            var followerCount = await db.UserFollows.CountAsync(f => f.FollowingId == userId);
            return Results.Ok(new { FollowerCount = followerCount });
        }).RequireAuthorization();

        // Unfollow user
        group.MapDelete("/follow", async (int userId, ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            var followerId = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var follow = await db.UserFollows.FirstOrDefaultAsync(f => f.FollowerId == followerId && f.FollowingId == userId);
            if (follow is null) return Results.NotFound();

            db.UserFollows.Remove(follow);
            await db.SaveChangesAsync();

            var followerCount = await db.UserFollows.CountAsync(f => f.FollowingId == userId);
            return Results.Ok(new { FollowerCount = followerCount });
        }).RequireAuthorization();

        // Get followers
        group.MapGet("/followers", async (int userId, CrimeCodeDbContext db) =>
        {
            var ranks = await db.UserRanks.OrderByDescending(r => r.MinPosts).ToListAsync();
            var followers = await db.UserFollows
                .Where(f => f.FollowingId == userId)
                .Include(f => f.Follower).ThenInclude(u => u.Posts)
                .OrderByDescending(f => f.CreatedAt)
                .ToListAsync();

            var result = followers.Select(f =>
            {
                var rank = LeaderboardEndpoints.GetRank(f.Follower, ranks);
                return new FollowDto(f.Follower.Id, f.Follower.Username, f.Follower.AvatarUrl, rank.Name, rank.Color, f.CreatedAt);
            }).ToList();

            return Results.Ok(result);
        });

        // Get following
        group.MapGet("/following", async (int userId, CrimeCodeDbContext db) =>
        {
            var ranks = await db.UserRanks.OrderByDescending(r => r.MinPosts).ToListAsync();
            var following = await db.UserFollows
                .Where(f => f.FollowerId == userId)
                .Include(f => f.Following).ThenInclude(u => u.Posts)
                .OrderByDescending(f => f.CreatedAt)
                .ToListAsync();

            var result = following.Select(f =>
            {
                var rank = LeaderboardEndpoints.GetRank(f.Following, ranks);
                return new FollowDto(f.Following.Id, f.Following.Username, f.Following.AvatarUrl, rank.Name, rank.Color, f.CreatedAt);
            }).ToList();

            return Results.Ok(result);
        });
    }
}
