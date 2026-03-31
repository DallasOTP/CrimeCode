using System.Security.Claims;
using CrimeCode.Data;
using CrimeCode.DTOs;
using CrimeCode.Models;
using Microsoft.EntityFrameworkCore;

namespace CrimeCode.Endpoints;

public static class ShoutboxEndpoints
{
    public static void MapShoutboxEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/shoutbox");

        // Get recent shoutbox messages
        group.MapGet("/", async (CrimeCodeDbContext db) =>
        {
            var messages = await db.ShoutboxMessages
                .Include(s => s.Author)
                .OrderByDescending(s => s.CreatedAt)
                .Take(50)
                .Select(s => new ShoutboxDto(s.Id, s.Content, s.CreatedAt,
                    s.Author.Username, s.AuthorId, s.Author.AvatarUrl, s.Author.Role))
                .ToListAsync();

            // Return in chronological order for display
            messages.Reverse();
            return Results.Ok(messages);
        });

        // Post a shoutbox message
        group.MapPost("/", async (ShoutboxPostRequest req, ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            var userId = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);

            if (string.IsNullOrWhiteSpace(req.Content) || req.Content.Length > 300)
                return Results.BadRequest(new { error = "Il messaggio deve essere tra 1 e 300 caratteri" });

            var message = new ShoutboxMessage
            {
                Content = req.Content,
                AuthorId = userId
            };

            db.ShoutboxMessages.Add(message);

            // Award 1 credit for shoutbox activity 
            var user = await db.Users.FindAsync(userId);
            if (user is not null)
            {
                user.Credits += 1;
                db.CreditTransactions.Add(new CreditTransaction
                {
                    UserId = userId, Amount = 1, Type = "Earn", Reason = "Messaggio shoutbox"
                });
            }

            await db.SaveChangesAsync();

            return Results.Ok(new { id = message.Id });
        }).RequireAuthorization();

        // Delete shoutbox message (admin/mod only)
        group.MapDelete("/{id:int}", async (int id, ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            var role = principal.FindFirstValue(ClaimTypes.Role);
            if (role != "Admin" && role != "Moderator")
                return Results.Forbid();

            var msg = await db.ShoutboxMessages.FindAsync(id);
            if (msg is null) return Results.NotFound();

            db.ShoutboxMessages.Remove(msg);
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization();
    }
}
