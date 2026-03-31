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
            var followers = await db.UserFollows
                .Where(f => f.FollowingId == userId)
                .Include(f => f.Follower)
                .OrderByDescending(f => f.CreatedAt)
                .Select(f => new FollowDto(f.Follower.Id, f.Follower.Username, f.Follower.AvatarUrl, f.CreatedAt))
                .ToListAsync();

            return Results.Ok(followers);
        });

        // Get following
        group.MapGet("/following", async (int userId, CrimeCodeDbContext db) =>
        {
            var following = await db.UserFollows
                .Where(f => f.FollowerId == userId)
                .Include(f => f.Following)
                .OrderByDescending(f => f.CreatedAt)
                .Select(f => new FollowDto(f.Following.Id, f.Following.Username, f.Following.AvatarUrl, f.CreatedAt))
                .ToListAsync();

            return Results.Ok(following);
        });
    }
}
