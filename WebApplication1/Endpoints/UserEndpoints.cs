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

        group.MapGet("/{id:int}", async (int id, CrimeCodeDbContext db, ClaimsPrincipal principal) =>
        {
            var user = await db.Users
                .Include(u => u.Followers)
                .Include(u => u.Following)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (user is null) return Results.NotFound();

            var currentUserId = int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var uid) ? uid : 0;
            var followedByCurrentUser = currentUserId > 0 && user.Followers.Any(f => f.FollowerId == currentUserId);

            return Results.Ok(new UserProfile(user.Id, user.Username, user.AvatarUrl, user.Bio, user.Signature,
                user.Role, user.CustomTitle, user.CreatedAt, user.LastSeenAt,
                user.Credits, user.ReputationScore,
                user.Status ?? "offline", user.Followers.Count, user.Following.Count, followedByCurrentUser,
                user.BannerUrl, user.Website, user.Location, user.Jabber, user.Birthday));
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
            if (req.BannerUrl is not null) user.BannerUrl = req.BannerUrl.Length > 500 ? req.BannerUrl[..500] : req.BannerUrl;
            if (req.Website is not null) user.Website = req.Website.Length > 200 ? req.Website[..200] : req.Website;
            if (req.Location is not null) user.Location = req.Location.Length > 100 ? req.Location[..100] : req.Location;
            if (req.Jabber is not null) user.Jabber = req.Jabber.Length > 100 ? req.Jabber[..100] : req.Jabber;
            if (req.Birthday.HasValue) user.Birthday = req.Birthday;

            await db.SaveChangesAsync();
            return Results.Ok(new { user.Bio, user.Signature, user.BannerUrl, user.Website, user.Location, user.Jabber, user.Birthday });
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

public record UpdateProfileRequest(string? Bio, string? Signature, string? BannerUrl, string? Website, string? Location, string? Jabber, DateTime? Birthday);
