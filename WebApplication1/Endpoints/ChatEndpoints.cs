using System.Security.Claims;
using CrimeCode.Data;
using CrimeCode.DTOs;
using CrimeCode.Models;
using Microsoft.EntityFrameworkCore;

namespace CrimeCode.Endpoints;

public static class ChatEndpoints
{
    public static void MapChatEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/chat").RequireAuthorization().RequireRateLimiting("api-write");

        // Get online users available for chat (excludes current user)
        group.MapGet("/contacts", async (ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            var userId = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var threshold = DateTime.UtcNow.AddMinutes(-15);

            var onlineUsers = await db.Users
                .Where(u => u.Id != userId && !u.IsBanned && u.LastSeenAt >= threshold)
                .OrderByDescending(u => u.LastSeenAt)
                .ToListAsync();

            var chatContacts = new List<ChatContactDto>();
            foreach (var u in onlineUsers)
            {
                var lastMsg = await db.ChatMessages
                    .Where(m => (m.SenderId == userId && m.ReceiverId == u.Id) ||
                                (m.SenderId == u.Id && m.ReceiverId == userId))
                    .OrderByDescending(m => m.CreatedAt)
                    .FirstOrDefaultAsync();

                var unread = await db.ChatMessages
                    .CountAsync(m => m.SenderId == u.Id && m.ReceiverId == userId && !m.IsRead);

                var preview = lastMsg != null
                    ? (lastMsg.Content.Length > 50 ? lastMsg.Content[..50] + "..." : lastMsg.Content)
                    : null;

                chatContacts.Add(new ChatContactDto(
                    u.Id, u.Username, u.AvatarUrl, u.Status,
                    preview, lastMsg?.CreatedAt, unread));
            }

            return Results.Ok(chatContacts);
        });

        // Get chat history with a specific user
        group.MapGet("/{otherUserId:int}", async (int otherUserId, ClaimsPrincipal principal, CrimeCodeDbContext db, int? before) =>
        {
            var userId = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var query = db.ChatMessages
                .Where(m =>
                    (m.SenderId == userId && m.ReceiverId == otherUserId) ||
                    (m.SenderId == otherUserId && m.ReceiverId == userId));

            if (before.HasValue)
                query = query.Where(m => m.Id < before.Value);

            var messages = await query
                .OrderByDescending(m => m.CreatedAt)
                .Take(50)
                .Include(m => m.Sender)
                .Include(m => m.Receiver)
                .OrderBy(m => m.CreatedAt)
                .Select(m => new ChatMessageDto(m.Id, m.Content, m.CreatedAt, m.IsRead,
                    m.SenderId, m.Sender.Username, m.Sender.AvatarUrl,
                    m.ReceiverId, m.Receiver.Username))
                .ToListAsync();

            // Mark received messages as read
            var unread = await db.ChatMessages
                .Where(m => m.SenderId == otherUserId && m.ReceiverId == userId && !m.IsRead)
                .ToListAsync();
            foreach (var m in unread) m.IsRead = true;
            if (unread.Count > 0) await db.SaveChangesAsync();

            return Results.Ok(messages);
        });

        // Send a chat message
        group.MapPost("/{otherUserId:int}", async (int otherUserId, SendChatRequest req, ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            var userId = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);

            if (otherUserId == userId)
                return Results.BadRequest(new { error = "Non puoi chattare con te stesso" });

            if (string.IsNullOrWhiteSpace(req.Content) || req.Content.Length > 1000)
                return Results.BadRequest(new { error = "Messaggio non valido (max 1000 caratteri)" });

            var receiver = await db.Users.FindAsync(otherUserId);
            if (receiver is null)
                return Results.BadRequest(new { error = "Utente non trovato" });

            var sender = await db.Users.FindAsync(userId);

            var msg = new ChatMessage
            {
                Content = req.Content.Trim(),
                SenderId = userId,
                ReceiverId = otherUserId
            };
            db.ChatMessages.Add(msg);
            await db.SaveChangesAsync();

            return Results.Ok(new ChatMessageDto(msg.Id, msg.Content, msg.CreatedAt, msg.IsRead,
                msg.SenderId, sender!.Username, sender.AvatarUrl,
                msg.ReceiverId, receiver.Username));
        });

        // Get new messages since a given ID (for polling)
        group.MapGet("/{otherUserId:int}/new", async (int otherUserId, int afterId, ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            var userId = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var messages = await db.ChatMessages
                .Where(m => m.Id > afterId &&
                    ((m.SenderId == userId && m.ReceiverId == otherUserId) ||
                     (m.SenderId == otherUserId && m.ReceiverId == userId)))
                .OrderBy(m => m.CreatedAt)
                .Include(m => m.Sender)
                .Include(m => m.Receiver)
                .Select(m => new ChatMessageDto(m.Id, m.Content, m.CreatedAt, m.IsRead,
                    m.SenderId, m.Sender.Username, m.Sender.AvatarUrl,
                    m.ReceiverId, m.Receiver.Username))
                .ToListAsync();

            // Mark received as read
            var unread = await db.ChatMessages
                .Where(m => m.Id > afterId && m.SenderId == otherUserId && m.ReceiverId == userId && !m.IsRead)
                .ToListAsync();
            foreach (var m in unread) m.IsRead = true;
            if (unread.Count > 0) await db.SaveChangesAsync();

            return Results.Ok(messages);
        });

        // Get total unread chat count
        group.MapGet("/unread-count", async (ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            var userId = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var count = await db.ChatMessages.CountAsync(m => m.ReceiverId == userId && !m.IsRead);
            return Results.Ok(new { count });
        });
    }
}
