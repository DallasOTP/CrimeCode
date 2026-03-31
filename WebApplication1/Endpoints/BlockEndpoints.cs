using System.Security.Claims;
using CrimeCode.Data;
using CrimeCode.Models;
using Microsoft.EntityFrameworkCore;

namespace CrimeCode.Endpoints;

public static class BlockEndpoints
{
    public static void MapBlockEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/users").RequireAuthorization();

        group.MapGet("/blocked", async (ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            var userId = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var blocked = await db.UserBlocks
                .Where(b => b.BlockerId == userId)
                .Join(db.Users, b => b.BlockedId, u => u.Id, (b, u) => new { u.Id, u.Username, u.AvatarUrl })
                .ToListAsync();
            return Results.Ok(blocked);
        });

        group.MapPost("/block", async (BlockUserRequest req, ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            var userId = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            if (string.IsNullOrWhiteSpace(req.Username))
                return Results.BadRequest(new { error = "Username required" });

            var target = await db.Users.FirstOrDefaultAsync(u => u.Username == req.Username);
            if (target is null) return Results.NotFound(new { error = "User not found" });
            if (target.Id == userId) return Results.BadRequest(new { error = "Cannot block yourself" });

            var exists = await db.UserBlocks.AnyAsync(b => b.BlockerId == userId && b.BlockedId == target.Id);
            if (exists) return Results.Ok(new { message = "Already blocked" });

            db.UserBlocks.Add(new UserBlock { BlockerId = userId, BlockedId = target.Id });
            await db.SaveChangesAsync();
            return Results.Ok(new { message = "User blocked" });
        });

        group.MapPost("/unblock", async (BlockUserRequest req, ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            var userId = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            if (string.IsNullOrWhiteSpace(req.Username))
                return Results.BadRequest(new { error = "Username required" });

            var target = await db.Users.FirstOrDefaultAsync(u => u.Username == req.Username);
            if (target is null) return Results.NotFound(new { error = "User not found" });

            var block = await db.UserBlocks.FirstOrDefaultAsync(b => b.BlockerId == userId && b.BlockedId == target.Id);
            if (block is null) return Results.NotFound(new { error = "User not blocked" });

            db.UserBlocks.Remove(block);
            await db.SaveChangesAsync();
            return Results.Ok(new { message = "User unblocked" });
        });
    }
}

public record BlockUserRequest(string Username);
