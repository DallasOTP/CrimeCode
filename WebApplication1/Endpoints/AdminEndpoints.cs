using System.Security.Claims;
using CrimeCode.Data;
using CrimeCode.DTOs;
using CrimeCode.Models;
using Microsoft.EntityFrameworkCore;

namespace CrimeCode.Endpoints;

public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin").RequireAuthorization();

        // List all users
        group.MapGet("/users", async (int? page, int? pageSize, string? search, ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            if (!IsAdmin(principal)) return Results.Forbid();

            var p = page ?? 1;
            var ps = Math.Min(pageSize ?? 20, 50);

            var query = db.Users
                .Include(u => u.Threads)
                .Include(u => u.Posts)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(u => u.Username.Contains(search) || u.Email.Contains(search));

            var total = await query.CountAsync();
            var users = await query
                .OrderByDescending(u => u.CreatedAt)
                .Skip((p - 1) * ps)
                .Take(ps)
                .Select(u => new AdminUserDto(u.Id, u.Username, u.Email, u.Role, u.CreatedAt, u.Threads.Count, u.Posts.Count, u.Credits, u.ReputationScore, u.IsBanned))
                .ToListAsync();

            return Results.Ok(new { users, total, page = p, pageSize = ps });
        });

        // Change user role
        group.MapPut("/users/{id:int}/role", async (int id, UpdateUserRoleRequest req, ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            if (!IsAdmin(principal)) return Results.Forbid();

            var validRoles = new[] { "Member", "Moderator", "Admin" };
            if (!validRoles.Contains(req.Role))
                return Results.BadRequest(new { error = "Ruolo non valido. Usa: Member, Moderator, Admin" });

            var user = await db.Users.FindAsync(id);
            if (user is null) return Results.NotFound();

            var currentUserId = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            if (user.Id == currentUserId)
                return Results.BadRequest(new { error = "Non puoi modificare il tuo stesso ruolo" });

            user.Role = req.Role;
            await db.SaveChangesAsync();

            return Results.Ok(new { user.Id, user.Username, user.Role });
        });

        // Ban user (delete)
        group.MapDelete("/users/{id:int}", async (int id, ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            if (!IsAdmin(principal)) return Results.Forbid();

            var currentUserId = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            if (id == currentUserId)
                return Results.BadRequest(new { error = "Non puoi eliminare te stesso" });

            var user = await db.Users.FindAsync(id);
            if (user is null) return Results.NotFound();

            // Remove user's likes, posts replies, posts, threads
            var userPostIds = await db.Posts.Where(p => p.AuthorId == id).Select(p => p.Id).ToListAsync();
            var likesToRemove = await db.PostLikes.Where(l => l.UserId == id || userPostIds.Contains(l.PostId)).ToListAsync();
            db.PostLikes.RemoveRange(likesToRemove);

            var repliesToRemove = await db.Posts.Where(p => p.ParentPostId != null && userPostIds.Contains(p.ParentPostId.Value)).ToListAsync();
            db.Posts.RemoveRange(repliesToRemove);

            var postsToRemove = await db.Posts.Where(p => p.AuthorId == id).ToListAsync();
            db.Posts.RemoveRange(postsToRemove);

            var threadsToRemove = await db.Threads.Where(t => t.AuthorId == id).ToListAsync();
            db.Threads.RemoveRange(threadsToRemove);

            var notificationsToRemove = await db.Notifications.Where(n => n.UserId == id || n.FromUserId == id).ToListAsync();
            db.Notifications.RemoveRange(notificationsToRemove);

            var messagesToRemove = await db.PrivateMessages.Where(m => m.SenderId == id || m.ReceiverId == id).ToListAsync();
            db.PrivateMessages.RemoveRange(messagesToRemove);

            db.Users.Remove(user);
            await db.SaveChangesAsync();

            return Results.NoContent();
        });

        // List all threads (admin view)
        group.MapGet("/threads", async (int? page, int? pageSize, string? search, ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            if (!IsAdmin(principal) && !IsModerator(principal)) return Results.Forbid();

            var p = page ?? 1;
            var ps = Math.Min(pageSize ?? 20, 50);

            var query = db.Threads
                .Include(t => t.Author)
                .Include(t => t.Category)
                .Include(t => t.Posts)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(t => t.Title.Contains(search));

            var total = await query.CountAsync();
            var threads = await query
                .OrderByDescending(t => t.CreatedAt)
                .Skip((p - 1) * ps)
                .Take(ps)
                .Select(t => new AdminThreadDto(t.Id, t.Title, t.Author.Username, t.Category.Name, t.Posts.Count, t.IsPinned, t.IsLocked, t.CreatedAt))
                .ToListAsync();

            return Results.Ok(new { threads, total, page = p, pageSize = ps });
        });

        // Pin/Lock thread
        group.MapPut("/threads/{id:int}", async (int id, UpdateThreadRequest req, ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            if (!IsAdmin(principal) && !IsModerator(principal)) return Results.Forbid();

            var thread = await db.Threads.FindAsync(id);
            if (thread is null) return Results.NotFound();

            if (req.IsPinned.HasValue) thread.IsPinned = req.IsPinned.Value;
            if (req.IsLocked.HasValue) thread.IsLocked = req.IsLocked.Value;

            await db.SaveChangesAsync();
            return Results.Ok(new { thread.Id, thread.IsPinned, thread.IsLocked });
        });

        // Delete thread
        group.MapDelete("/threads/{id:int}", async (int id, ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            if (!IsAdmin(principal) && !IsModerator(principal)) return Results.Forbid();

            var thread = await db.Threads
                .Include(t => t.Posts).ThenInclude(p => p.Likes)
                .Include(t => t.Posts).ThenInclude(p => p.Replies)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (thread is null) return Results.NotFound();

            foreach (var post in thread.Posts)
            {
                db.PostLikes.RemoveRange(post.Likes);
                db.Posts.RemoveRange(post.Replies);
            }
            db.Posts.RemoveRange(thread.Posts);
            db.Threads.Remove(thread);
            await db.SaveChangesAsync();

            return Results.NoContent();
        });

        // Stats dashboard
        group.MapGet("/stats", async (ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            if (!IsAdmin(principal)) return Results.Forbid();

            var cutoff = DateTime.UtcNow.AddMinutes(-15);

            return Results.Ok(new
            {
                totalUsers = await db.Users.CountAsync(),
                totalThreads = await db.Threads.CountAsync(),
                totalPosts = await db.Posts.CountAsync(),
                totalLikes = await db.PostLikes.CountAsync(),
                onlineUsers = await db.Users.CountAsync(u => u.LastSeenAt > cutoff),
                recentUsers = await db.Users.OrderByDescending(u => u.CreatedAt).Take(5)
                    .Select(u => new { u.Id, u.Username, u.CreatedAt }).ToListAsync(),
                recentThreads = await db.Threads.OrderByDescending(t => t.CreatedAt).Take(5)
                    .Include(t => t.Author)
                    .Select(t => new { t.Id, t.Title, Author = t.Author.Username, t.CreatedAt }).ToListAsync()
            });
        });

        // Ban user
        group.MapPut("/users/{id:int}/ban", async (int id, AdminBanRequest req, ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            if (!IsAdmin(principal)) return Results.Forbid();
            var user = await db.Users.FindAsync(id);
            if (user is null) return Results.NotFound();

            user.IsBanned = true;
            user.BanReason = req.Reason;
            await db.SaveChangesAsync();
            return Results.Ok(new { user.Id, user.IsBanned });
        });

        // Unban user
        group.MapPut("/users/{id:int}/unban", async (int id, ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            if (!IsAdmin(principal)) return Results.Forbid();
            var user = await db.Users.FindAsync(id);
            if (user is null) return Results.NotFound();

            user.IsBanned = false;
            user.BanReason = null;
            await db.SaveChangesAsync();
            return Results.Ok(new { user.Id, user.IsBanned });
        });

        // Admin give/remove credits
        group.MapPut("/users/{id:int}/credits", async (int id, AdminCreditsRequest req, ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            if (!IsAdmin(principal)) return Results.Forbid();
            var user = await db.Users.FindAsync(id);
            if (user is null) return Results.NotFound();

            user.Credits += req.Amount;
            db.CreditTransactions.Add(new CreditTransaction
            {
                UserId = id, Amount = req.Amount, Type = "Admin", Reason = req.Reason
            });
            await db.SaveChangesAsync();
            return Results.Ok(new { user.Id, user.Credits });
        });
    }

    private static bool IsAdmin(ClaimsPrincipal principal) =>
        principal.FindFirstValue(ClaimTypes.Role) == "Admin";

    private static bool IsModerator(ClaimsPrincipal principal) =>
        principal.FindFirstValue(ClaimTypes.Role) is "Moderator" or "Admin";
}
