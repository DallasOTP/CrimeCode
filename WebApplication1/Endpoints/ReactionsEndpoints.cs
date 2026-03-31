using System.Security.Claims;
using CrimeCode.Data;
using CrimeCode.DTOs;
using CrimeCode.Models;
using Microsoft.EntityFrameworkCore;

namespace CrimeCode.Endpoints;

public static class ReactionsEndpoints
{
    private static readonly HashSet<string> AllowedEmojis = new()
    {
        "👍", "👎", "❤️", "😂", "😮", "😢", "🔥", "💀", "🤔", "👀", "🎯", "💯", "⚡", "🚀", "💎"
    };

    public static void MapReactionsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/posts/{postId:int}/reactions");

        // Add reaction
        group.MapPost("/", async (int postId, AddReactionRequest req, ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            var userId = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);

            if (!AllowedEmojis.Contains(req.Emoji))
                return Results.BadRequest("Emoji non consentita.");

            var post = await db.Posts.FindAsync(postId);
            if (post is null) return Results.NotFound();

            var existing = await db.PostReactions.FirstOrDefaultAsync(r => r.PostId == postId && r.UserId == userId && r.Emoji == req.Emoji);
            if (existing is not null)
                return Results.Conflict("Reazione già presente.");

            db.PostReactions.Add(new PostReaction
            {
                PostId = postId,
                UserId = userId,
                Emoji = req.Emoji,
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();

            var reactions = await db.PostReactions.Where(r => r.PostId == postId)
                .GroupBy(r => r.Emoji)
                .Select(g => new { Emoji = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Emoji, x => x.Count);

            return Results.Ok(reactions);
        }).RequireAuthorization();

        // Remove reaction
        group.MapDelete("/{emoji}", async (int postId, string emoji, ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            var userId = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var reaction = await db.PostReactions.FirstOrDefaultAsync(r => r.PostId == postId && r.UserId == userId && r.Emoji == emoji);
            if (reaction is null) return Results.NotFound();

            db.PostReactions.Remove(reaction);
            await db.SaveChangesAsync();

            var reactions = await db.PostReactions.Where(r => r.PostId == postId)
                .GroupBy(r => r.Emoji)
                .Select(g => new { Emoji = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Emoji, x => x.Count);

            return Results.Ok(reactions);
        }).RequireAuthorization();
    }
}
