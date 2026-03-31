using System.Security.Claims;
using CrimeCode.Data;
using CrimeCode.DTOs;
using CrimeCode.Models;
using Microsoft.EntityFrameworkCore;

namespace CrimeCode.Endpoints;

public static class ReputationEndpoints
{
    public static void MapReputationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/reputation");

        // Give reputation
        group.MapPost("/", async (GiveReputationRequest req, ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            var userId = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);

            if (req.UserId == userId)
                return Results.BadRequest(new { error = "Non puoi dare reputazione a te stesso" });

            if (req.Points != 1 && req.Points != -1)
                return Results.BadRequest(new { error = "Punti devono essere 1 o -1" });

            var receiver = await db.Users.FindAsync(req.UserId);
            if (receiver is null)
                return Results.NotFound(new { error = "Utente non trovato" });

            // Check if already given rep recently (within 24h)
            var recent = await db.Reputations
                .Where(r => r.GiverId == userId && r.ReceiverId == req.UserId && r.CreatedAt > DateTime.UtcNow.AddHours(-24))
                .AnyAsync();

            if (recent)
                return Results.BadRequest(new { error = "Puoi dare reputazione a questo utente solo una volta ogni 24 ore" });

            var rep = new Reputation
            {
                GiverId = userId,
                ReceiverId = req.UserId,
                Points = req.Points,
                Comment = req.Comment?.Length > 200 ? req.Comment[..200] : req.Comment
            };

            db.Reputations.Add(rep);
            receiver.ReputationScore += req.Points;
            await db.SaveChangesAsync();

            // Notify
            var giver = await db.Users.FindAsync(userId);
            var repType = req.Points > 0 ? "positiva" : "negativa";
            await NotificationEndpoints.CreateNotification(db, req.UserId, userId,
                "System", $"{giver!.Username} ti ha dato reputazione {repType}");

            return Results.Ok(new { receiver.ReputationScore });
        }).RequireAuthorization();

        // Get reputation history for user
        group.MapGet("/{userId:int}", async (int userId, CrimeCodeDbContext db) =>
        {
            var reps = await db.Reputations
                .Where(r => r.ReceiverId == userId)
                .Include(r => r.Giver)
                .Include(r => r.Receiver)
                .OrderByDescending(r => r.CreatedAt)
                .Take(50)
                .Select(r => new ReputationDto(r.Id, r.Points, r.Comment, r.CreatedAt,
                    r.Giver.Username, r.GiverId, r.Receiver.Username, r.ReceiverId))
                .ToListAsync();

            return Results.Ok(reps);
        });
    }
}
