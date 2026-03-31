using System.Security.Claims;
using CrimeCode.Data;
using CrimeCode.DTOs;
using CrimeCode.Models;
using Microsoft.EntityFrameworkCore;

namespace CrimeCode.Endpoints;

public static class MessageEndpoints
{
    public static void MapMessageEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/messages").RequireAuthorization();

        // Get conversations (inbox)
        group.MapGet("/conversations", async (ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            var userId = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var messages = await db.PrivateMessages
                .Where(m => m.SenderId == userId || m.ReceiverId == userId)
                .Include(m => m.Sender)
                .Include(m => m.Receiver)
                .OrderByDescending(m => m.CreatedAt)
                .ToListAsync();

            var conversations = messages
                .GroupBy(m => m.SenderId == userId ? m.ReceiverId : m.SenderId)
                .Select(g =>
                {
                    var otherUserId = g.Key;
                    var last = g.First();
                    var otherUser = last.SenderId == userId ? last.Receiver : last.Sender;
                    var unread = g.Count(m => m.ReceiverId == userId && !m.IsRead);
                    var preview = last.Content.Length > 80 ? last.Content[..80] + "..." : last.Content;

                    return new ConversationDto(otherUserId, otherUser.Username, otherUser.AvatarUrl, preview, last.CreatedAt, unread);
                })
                .OrderByDescending(c => c.LastMessageAt)
                .ToList();

            return Results.Ok(conversations);
        });

        // Get messages with specific user
        group.MapGet("/{otherUserId:int}", async (int otherUserId, ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            var userId = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var messages = await db.PrivateMessages
                .Where(m =>
                    (m.SenderId == userId && m.ReceiverId == otherUserId) ||
                    (m.SenderId == otherUserId && m.ReceiverId == userId))
                .Include(m => m.Sender)
                .Include(m => m.Receiver)
                .OrderBy(m => m.CreatedAt)
                .Select(m => new MessageDto(m.Id, m.Content, m.IsRead, m.CreatedAt,
                    m.SenderId, m.Sender.Username, m.Sender.AvatarUrl,
                    m.ReceiverId, m.Receiver.Username))
                .ToListAsync();

            // Mark received messages as read
            var unread = await db.PrivateMessages
                .Where(m => m.SenderId == otherUserId && m.ReceiverId == userId && !m.IsRead)
                .ToListAsync();
            foreach (var m in unread) m.IsRead = true;
            if (unread.Count > 0) await db.SaveChangesAsync();

            return Results.Ok(messages);
        });

        // Send message
        group.MapPost("/", async (SendMessageRequest req, ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            var userId = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);

            if (req.ReceiverId == userId)
                return Results.BadRequest(new { error = "Non puoi inviare messaggi a te stesso" });

            if (string.IsNullOrWhiteSpace(req.Content))
                return Results.BadRequest(new { error = "Il messaggio non può essere vuoto" });

            var receiver = await db.Users.FindAsync(req.ReceiverId);
            if (receiver is null)
                return Results.BadRequest(new { error = "Utente destinatario non trovato" });

            var sender = await db.Users.FindAsync(userId);

            var message = new PrivateMessage
            {
                SenderId = userId,
                ReceiverId = req.ReceiverId,
                Content = req.Content
            };

            db.PrivateMessages.Add(message);
            await db.SaveChangesAsync();

            // Create notification
            await NotificationEndpoints.CreateNotification(db, req.ReceiverId, userId,
                "Message", $"{sender!.Username} ti ha inviato un messaggio");

            return Results.Created($"/api/messages/{req.ReceiverId}", new { id = message.Id });
        });

        // Get total unread messages count
        group.MapGet("/unread-count", async (ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            var userId = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var count = await db.PrivateMessages.CountAsync(m => m.ReceiverId == userId && !m.IsRead);
            return Results.Ok(new { count });
        });
    }
}
