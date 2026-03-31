using System.Security.Claims;
using CrimeCode.Data;
using CrimeCode.DTOs;

namespace CrimeCode.Endpoints;

public static class StatusEndpoints
{
    private static readonly HashSet<string> AllowedStatuses = new() { "online", "away", "busy", "offline" };

    public static void MapStatusEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/users");

        group.MapPut("/status", async (UpdateStatusRequest req, ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            var userId = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);

            if (!AllowedStatuses.Contains(req.Status))
                return Results.BadRequest("Stato non valido. Usa: online, away, busy, offline.");

            var user = await db.Users.FindAsync(userId);
            if (user is null) return Results.NotFound();

            user.Status = req.Status;
            user.LastSeenAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            return Results.Ok(new { user.Status });
        }).RequireAuthorization();
    }
}
