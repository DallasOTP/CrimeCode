using System.Security.Claims;
using CrimeCode.Data;
using CrimeCode.DTOs;
using CrimeCode.Models;
using Microsoft.EntityFrameworkCore;

namespace CrimeCode.Endpoints;

public static class ThreadEndpoints
{
    public static void MapThreadEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/threads");

        // List threads (optionally filtered by category)
        group.MapGet("/", async (int? categoryId, int page, int pageSize, CrimeCodeDbContext db) =>
        {
            page = page < 1 ? 1 : page;
            pageSize = pageSize < 1 ? 20 : Math.Min(pageSize, 50);

            var query = db.Threads
                .Include(t => t.Author)
                .Include(t => t.Category)
                .Include(t => t.Posts).ThenInclude(p => p.Author)
                .Include(t => t.Tag)
                .AsQueryable();

            if (categoryId.HasValue)
            {
                // Also include threads from subcategories
                var subCatIds = await db.Categories
                    .Where(c => c.ParentId == categoryId.Value)
                    .Select(c => c.Id)
                    .ToListAsync();
                var allCatIds = new List<int> { categoryId.Value };
                allCatIds.AddRange(subCatIds);
                query = query.Where(t => allCatIds.Contains(t.CategoryId));
            }

            var total = await query.CountAsync();

            var threads = await query
                .OrderByDescending(t => t.IsPinned)
                .ThenByDescending(t => t.LastActivityAt ?? t.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var result = threads.Select(t =>
            {
                var lastPost = t.Posts.OrderByDescending(p => p.CreatedAt).FirstOrDefault();
                return new ThreadSummaryDto(
                    t.Id, t.Title, t.Prefix, t.Author.Username, t.AuthorId, t.Author.AvatarUrl,
                    t.Category.Name, t.Posts.Count, t.ViewCount, t.IsPinned, t.IsLocked,
                    t.CreatedAt, t.LastActivityAt,
                    t.Tag?.Name, t.Tag?.Color,
                    lastPost?.Author?.Username, lastPost?.CreatedAt);
            }).ToList();

            return Results.Ok(new { threads = result, total, page, pageSize });
        });

        // Get thread detail
        group.MapGet("/{id:int}", async (int id, ClaimsPrincipal? principal, CrimeCodeDbContext db) =>
        {
            var thread = await db.Threads
                .Include(t => t.Author)
                .Include(t => t.Category)
                .Include(t => t.Tag)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (thread is null) return Results.NotFound();

            thread.ViewCount++;
            await db.SaveChangesAsync();

            // Update user LastSeenAt
            var userIdStr = principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            int currentUserId = 0;
            if (userIdStr is not null && int.TryParse(userIdStr, out currentUserId))
            {
                var user = await db.Users.FindAsync(currentUserId);
                if (user is not null) { user.LastSeenAt = DateTime.UtcNow; await db.SaveChangesAsync(); }
            }

            var ranks = await db.UserRanks.OrderByDescending(r => r.MinPosts).ToListAsync();

            var posts = await db.Posts
                .Where(p => p.ThreadId == id && p.ParentPostId == null)
                .Include(p => p.Author).ThenInclude(a => a.Posts)
                .Include(p => p.Likes)
                .Include(p => p.Replies).ThenInclude(r => r.Author).ThenInclude(a => a!.Posts)
                .Include(p => p.Replies).ThenInclude(r => r.Likes)
                .OrderBy(p => p.CreatedAt)
                .ToListAsync();

            var postDtos = posts.Select(p => MapPost(p, currentUserId, ranks)).ToList();

            return Results.Ok(new ThreadDetailDto(
                thread.Id, thread.Title, thread.Prefix, thread.Author.Username, thread.AuthorId, thread.Author.AvatarUrl,
                thread.Category.Name, thread.CategoryId, thread.ViewCount,
                thread.IsPinned, thread.IsLocked, thread.CreatedAt, postDtos,
                thread.Tag?.Name, thread.Tag?.Color));
        });

        // Create thread
        group.MapPost("/", async (CreateThreadRequest req, ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            var userId = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);

            if (string.IsNullOrWhiteSpace(req.Title) || req.Title.Length < 3)
                return Results.BadRequest(new { error = "Il titolo deve avere almeno 3 caratteri" });

            if (string.IsNullOrWhiteSpace(req.Content))
                return Results.BadRequest(new { error = "Il contenuto non può essere vuoto" });

            var category = await db.Categories.FindAsync(req.CategoryId);
            if (category is null)
                return Results.BadRequest(new { error = "Categoria non trovata" });

            var thread = new ForumThread
            {
                Title = req.Title,
                Prefix = req.Prefix,
                AuthorId = userId,
                CategoryId = req.CategoryId,
                TagId = req.TagId,
                LastActivityAt = DateTime.UtcNow
            };
            db.Threads.Add(thread);
            await db.SaveChangesAsync();

            var post = new Post
            {
                Content = req.Content,
                AuthorId = userId,
                ThreadId = thread.Id
            };
            db.Posts.Add(post);

            // Award credits for new thread
            var user = await db.Users.FindAsync(userId);
            if (user is not null)
            {
                user.Credits += 5;
                db.CreditTransactions.Add(new CreditTransaction
                {
                    UserId = userId, Amount = 5, Type = "Earn", Reason = "Nuovo thread creato"
                });
            }

            await db.SaveChangesAsync();

            return Results.Created($"/api/threads/{thread.Id}", new { id = thread.Id });
        }).RequireAuthorization();

        // Search threads
        group.MapGet("/search", async (string q, CrimeCodeDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
                return Results.BadRequest(new { error = "Query troppo corta" });

            var threads = await db.Threads
                .Include(t => t.Author)
                .Include(t => t.Category)
                .Include(t => t.Posts).ThenInclude(p => p.Author)
                .Include(t => t.Tag)
                .Where(t => t.Title.Contains(q) || t.Posts.Any(p => p.Content.Contains(q)))
                .OrderByDescending(t => t.LastActivityAt ?? t.CreatedAt)
                .Take(20)
                .ToListAsync();

            var result = threads.Select(t =>
            {
                var lastPost = t.Posts.OrderByDescending(p => p.CreatedAt).FirstOrDefault();
                return new ThreadSummaryDto(
                    t.Id, t.Title, t.Prefix, t.Author.Username, t.AuthorId, t.Author.AvatarUrl,
                    t.Category.Name, t.Posts.Count, t.ViewCount, t.IsPinned, t.IsLocked,
                    t.CreatedAt, t.LastActivityAt,
                    t.Tag?.Name, t.Tag?.Color,
                    lastPost?.Author?.Username, lastPost?.CreatedAt);
            }).ToList();

            return Results.Ok(result);
        });
    }

    private static PostDto MapPost(Post p, int currentUserId, List<UserRank> ranks)
    {
        var rank = LeaderboardEndpoints.GetRank(p.Author, ranks);
        return new PostDto(
            p.Id, p.Content, p.Author.Username, p.AuthorId, p.Author.AvatarUrl,
            p.Author.Role, rank.Name, rank.Color, rank.Icon, p.Author.Signature,
            p.Author.Posts?.Count ?? 0, p.Author.ReputationScore, p.Author.CreatedAt,
            p.CreatedAt, p.EditedAt, p.Likes.Count,
            p.Likes.Any(l => l.UserId == currentUserId),
            p.ParentPostId,
            p.Replies.Select(r => MapPost(r, currentUserId, ranks)).ToList());
    }
}
