using System.Security.Claims;
using CrimeCode.Data;
using CrimeCode.DTOs;
using CrimeCode.Models;
using Microsoft.EntityFrameworkCore;

namespace CrimeCode.Endpoints;

public static class NotificationEndpoints
{
    public static void MapNotificationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/notifications").RequireAuthorization();

        // Get notifications
        group.MapGet("/", async (ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            var userId = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var notifications = await db.Notifications
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .Take(50)
                .Include(n => n.FromUser)
                .Select(n => new NotificationDto(
                    n.Id, n.Type, n.Message, n.IsRead, n.CreatedAt,
                    n.FromUser != null ? n.FromUser.Username : null,
                    n.ThreadId, n.PostId))
                .ToListAsync();

            return Results.Ok(notifications);
        });

        // Get unread count
        group.MapGet("/unread-count", async (ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            var userId = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var count = await db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);
            return Results.Ok(new { count });
        });

        // Mark as read
        group.MapPut("/{id:int}/read", async (int id, ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            var userId = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var notif = await db.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);
            if (notif is null) return Results.NotFound();

            notif.IsRead = true;
            await db.SaveChangesAsync();
            return Results.Ok();
        });

        // Mark all as read
        group.MapPut("/read-all", async (ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            var userId = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var unread = await db.Notifications.Where(n => n.UserId == userId && !n.IsRead).ToListAsync();
            foreach (var n in unread) n.IsRead = true;
            await db.SaveChangesAsync();
            return Results.Ok(new { marked = unread.Count });
        });

        // Delete notification
        group.MapDelete("/{id:int}", async (int id, ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            var userId = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var notif = await db.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);
            if (notif is null) return Results.NotFound();

            db.Notifications.Remove(notif);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }

    // Helper to create notifications from other endpoints
    public static async Task CreateNotification(CrimeCodeDbContext db, int userId, int? fromUserId, string type, string message, int? threadId = null, int? postId = null)
    {
        if (userId == fromUserId) return; // Don't notify self

        db.Notifications.Add(new Notification
        {
            UserId = userId,
            FromUserId = fromUserId,
            Type = type,
            Message = message,
            ThreadId = threadId,
            PostId = postId
        });
        await db.SaveChangesAsync();
    }
}
