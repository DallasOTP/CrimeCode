using System.Security.Claims;
using CrimeCode.Data;
using CrimeCode.DTOs;
using Microsoft.EntityFrameworkCore;

namespace CrimeCode.Endpoints;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/users");

        group.MapGet("/{id:int}", async (int id, CrimeCodeDbContext db) =>
        {
            var user = await db.Users
                .Include(u => u.Threads)
                .Include(u => u.Posts)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (user is null) return Results.NotFound();

            var ranks = await db.UserRanks.OrderByDescending(r => r.MinPosts).ToListAsync();
            var rank = LeaderboardEndpoints.GetRank(user, ranks);

            return Results.Ok(new UserProfile(user.Id, user.Username, user.AvatarUrl, user.Bio, user.Signature,
                user.Role, user.CustomTitle, user.CreatedAt, user.LastSeenAt,
                user.Threads.Count, user.Posts.Count, user.Credits, user.ReputationScore,
                rank.Name, rank.Color, rank.Icon));
        });

        group.MapGet("/{id:int}/posts", async (int id, CrimeCodeDbContext db) =>
        {
            var posts = await db.Posts
                .Where(p => p.AuthorId == id)
                .Include(p => p.Thread)
                .OrderByDescending(p => p.CreatedAt)
                .Take(20)
                .Select(p => new
                {
                    p.Id,
                    p.Content,
                    p.CreatedAt,
                    ThreadId = p.Thread.Id,
                    ThreadTitle = p.Thread.Title
                })
                .ToListAsync();

            return Results.Ok(posts);
        });

        // Update bio/signature
        group.MapPut("/{id:int}/profile", async (int id, UpdateProfileRequest req, ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            var userId = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            if (userId != id) return Results.Forbid();

            var user = await db.Users.FindAsync(id);
            if (user is null) return Results.NotFound();

            if (req.Bio is not null) user.Bio = req.Bio.Length > 500 ? req.Bio[..500] : req.Bio;
            if (req.Signature is not null) user.Signature = req.Signature.Length > 200 ? req.Signature[..200] : req.Signature;

            await db.SaveChangesAsync();
            return Results.Ok(new { user.Bio, user.Signature });
        }).RequireAuthorization();

        // Heartbeat to update LastSeenAt
        group.MapPost("/heartbeat", async (ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            var userIdStr = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdStr is null || !int.TryParse(userIdStr, out var userId))
                return Results.Unauthorized();

            var user = await db.Users.FindAsync(userId);
            if (user is not null)
            {
                user.LastSeenAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
            }
            return Results.Ok();
        }).RequireAuthorization();
    }
}

public record UpdateProfileRequest(string? Bio, string? Signature);
