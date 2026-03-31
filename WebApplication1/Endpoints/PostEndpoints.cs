using System.Security.Claims;
using CrimeCode.Data;
using CrimeCode.DTOs;
using CrimeCode.Models;
using Microsoft.EntityFrameworkCore;

namespace CrimeCode.Endpoints;

public static class PostEndpoints
{
    public static void MapPostEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/threads/{threadId:int}/posts");

        // Add post to thread
        group.MapPost("/", async (int threadId, CreatePostRequest req, ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            var userId = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var thread = await db.Threads.FindAsync(threadId);
            if (thread is null) return Results.NotFound();
            if (thread.IsLocked)
                return Results.BadRequest(new { error = "Questo thread è chiuso" });

            if (string.IsNullOrWhiteSpace(req.Content))
                return Results.BadRequest(new { error = "Il contenuto non può essere vuoto" });

            if (req.ParentPostId.HasValue)
            {
                var parent = await db.Posts.FindAsync(req.ParentPostId.Value);
                if (parent is null || parent.ThreadId != threadId)
                    return Results.BadRequest(new { error = "Post padre non trovato" });
            }

            var post = new Post
            {
                Content = req.Content,
                AuthorId = userId,
                ThreadId = threadId,
                ParentPostId = req.ParentPostId
            };
            db.Posts.Add(post);

            thread.LastActivityAt = DateTime.UtcNow;

            // Award credits for posting
            var postUser = await db.Users.FindAsync(userId);
            if (postUser is not null)
            {
                postUser.Credits += 2;
                db.CreditTransactions.Add(new CreditTransaction
                {
                    UserId = userId, Amount = 2, Type = "Earn", Reason = "Risposta nel forum"
                });
            }

            await db.SaveChangesAsync();

            // Notify thread author of new reply
            var sender = await db.Users.FindAsync(userId);
            if (thread.AuthorId != userId)
            {
                await NotificationEndpoints.CreateNotification(db, thread.AuthorId, userId,
                    "Reply", $"{sender!.Username} ha risposto al tuo thread \"{thread.Title}\"",
                    threadId, post.Id);
            }

            // Notify parent post author
            if (req.ParentPostId.HasValue)
            {
                var parentPost = await db.Posts.FindAsync(req.ParentPostId.Value);
                if (parentPost is not null && parentPost.AuthorId != userId && parentPost.AuthorId != thread.AuthorId)
                {
                    await NotificationEndpoints.CreateNotification(db, parentPost.AuthorId, userId,
                        "Reply", $"{sender!.Username} ha risposto al tuo commento",
                        threadId, post.Id);
                }
            }

            return Results.Created($"/api/threads/{threadId}/posts/{post.Id}", new { id = post.Id });
        }).RequireAuthorization();

        // Like/unlike a post
        group.MapPost("/{postId:int}/like", async (int threadId, int postId, ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            var userId = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var post = await db.Posts.FirstOrDefaultAsync(p => p.Id == postId && p.ThreadId == threadId);
            if (post is null) return Results.NotFound();

            var existing = await db.PostLikes.FirstOrDefaultAsync(l => l.UserId == userId && l.PostId == postId);
            if (existing is not null)
            {
                db.PostLikes.Remove(existing);
                await db.SaveChangesAsync();
                return Results.Ok(new { liked = false });
            }

            db.PostLikes.Add(new PostLike { UserId = userId, PostId = postId });
            await db.SaveChangesAsync();

            // Notify post author of like
            if (post.AuthorId != userId)
            {
                var liker = await db.Users.FindAsync(userId);
                await NotificationEndpoints.CreateNotification(db, post.AuthorId, userId,
                    "Like", $"{liker!.Username} ha messo like al tuo post",
                    threadId, postId);
            }

            return Results.Ok(new { liked = true });
        }).RequireAuthorization();

        // Edit post
        group.MapPut("/{postId:int}", async (int threadId, int postId, CreatePostRequest req, ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            var userId = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var role = principal.FindFirstValue(ClaimTypes.Role);

            var post = await db.Posts.FirstOrDefaultAsync(p => p.Id == postId && p.ThreadId == threadId);
            if (post is null) return Results.NotFound();

            if (post.AuthorId != userId && role != "Admin" && role != "Moderator")
                return Results.Forbid();

            post.Content = req.Content;
            post.EditedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            return Results.Ok(new { id = post.Id });
        }).RequireAuthorization();

        // Delete post
        group.MapDelete("/{postId:int}", async (int threadId, int postId, ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            var userId = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var role = principal.FindFirstValue(ClaimTypes.Role);

            var post = await db.Posts
                .Include(p => p.Replies)
                .Include(p => p.Likes)
                .FirstOrDefaultAsync(p => p.Id == postId && p.ThreadId == threadId);

            if (post is null) return Results.NotFound();

            if (post.AuthorId != userId && role != "Admin" && role != "Moderator")
                return Results.Forbid();

            db.PostLikes.RemoveRange(post.Likes);
            db.Posts.RemoveRange(post.Replies);
            db.Posts.Remove(post);
            await db.SaveChangesAsync();

            return Results.NoContent();
        }).RequireAuthorization();
    }
}
