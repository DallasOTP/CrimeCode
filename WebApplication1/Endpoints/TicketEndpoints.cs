using System.Security.Claims;
using CrimeCode.Data;
using CrimeCode.Models;
using Microsoft.EntityFrameworkCore;

namespace CrimeCode.Endpoints;

public static class TicketEndpoints
{
    public static void MapTicketEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/tickets").RequireAuthorization();

        // Create ticket
        group.MapPost("/", async (CreateTicketRequest req, ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            var userId = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);

            if (string.IsNullOrWhiteSpace(req.Subject) || req.Subject.Length > 200)
                return Results.BadRequest(new { error = "Oggetto obbligatorio (max 200 caratteri)" });
            if (string.IsNullOrWhiteSpace(req.Message) || req.Message.Length > 2000)
                return Results.BadRequest(new { error = "Messaggio obbligatorio (max 2000 caratteri)" });

            var ticket = new SupportTicket
            {
                Subject = req.Subject,
                Message = req.Message,
                Category = req.Category ?? "General",
                Priority = req.Priority ?? "Normal",
                UserId = userId
            };
            db.SupportTickets.Add(ticket);
            await db.SaveChangesAsync();
            return Results.Created($"/api/tickets/{ticket.Id}", new { id = ticket.Id });
        });

        // My tickets
        group.MapGet("/my", async (ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            var userId = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var tickets = await db.SupportTickets
                .Where(t => t.UserId == userId)
                .OrderByDescending(t => t.CreatedAt)
                .Select(t => new TicketDto(t.Id, t.Subject, t.Category, t.Status, t.Priority, t.CreatedAt, t.UpdatedAt, t.Replies.Count))
                .ToListAsync();
            return Results.Ok(tickets);
        });

        // Ticket detail
        group.MapGet("/{id:int}", async (int id, ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            var userId = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var role = principal.FindFirstValue(ClaimTypes.Role);

            var ticket = await db.SupportTickets
                .Include(t => t.User)
                .Include(t => t.Replies).ThenInclude(r => r.Author)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (ticket is null) return Results.NotFound();
            if (ticket.UserId != userId && role != "Admin" && role != "Moderator")
                return Results.Forbid();

            return Results.Ok(new TicketDetailDto(
                ticket.Id, ticket.Subject, ticket.Message, ticket.Category, ticket.Status, ticket.Priority,
                ticket.CreatedAt, ticket.UpdatedAt, ticket.User.Username,
                ticket.Replies.OrderBy(r => r.CreatedAt).Select(r => new TicketReplyDto(
                    r.Id, r.Message, r.IsStaff, r.CreatedAt, r.Author.Username, r.Author.AvatarUrl
                )).ToList()
            ));
        });

        // Reply to ticket
        group.MapPost("/{id:int}/reply", async (int id, TicketReplyRequest req, ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            var userId = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var role = principal.FindFirstValue(ClaimTypes.Role);

            var ticket = await db.SupportTickets.FirstOrDefaultAsync(t => t.Id == id);
            if (ticket is null) return Results.NotFound();
            if (ticket.UserId != userId && role != "Admin" && role != "Moderator")
                return Results.Forbid();

            if (string.IsNullOrWhiteSpace(req.Message) || req.Message.Length > 2000)
                return Results.BadRequest(new { error = "Messaggio obbligatorio (max 2000 caratteri)" });

            var isStaff = role is "Admin" or "Moderator";
            db.TicketReplies.Add(new TicketReply
            {
                TicketId = id,
                AuthorId = userId,
                Message = req.Message,
                IsStaff = isStaff
            });

            if (isStaff && ticket.Status == "Open")
                ticket.Status = "InProgress";
            ticket.UpdatedAt = DateTime.UtcNow;

            // Notify the other party
            var notifyUserId = isStaff ? ticket.UserId : ticket.AssignedToId;
            if (notifyUserId.HasValue || !isStaff)
            {
                db.Notifications.Add(new Notification
                {
                    UserId = isStaff ? ticket.UserId : 1, // notify user or admin
                    FromUserId = userId,
                    Type = "ticket_reply",
                    Message = $"Nuova risposta al ticket #{ticket.Id}: {ticket.Subject}"
                });
            }

            await db.SaveChangesAsync();
            return Results.Ok(new { id = ticket.Id });
        });

        // Close ticket (user or admin)
        group.MapPut("/{id:int}/close", async (int id, ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            var userId = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var role = principal.FindFirstValue(ClaimTypes.Role);

            var ticket = await db.SupportTickets.FirstOrDefaultAsync(t => t.Id == id);
            if (ticket is null) return Results.NotFound();
            if (ticket.UserId != userId && role != "Admin" && role != "Moderator")
                return Results.Forbid();

            ticket.Status = "Closed";
            ticket.ClosedAt = DateTime.UtcNow;
            ticket.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(new { status = ticket.Status });
        });

        // Admin: all tickets
        group.MapGet("/all", async (string? status, ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            var role = principal.FindFirstValue(ClaimTypes.Role);
            if (role != "Admin" && role != "Moderator") return Results.Forbid();

            var query = db.SupportTickets.Include(t => t.User).AsQueryable();
            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(t => t.Status == status);

            var tickets = await query
                .OrderByDescending(t => t.CreatedAt)
                .Select(t => new TicketAdminDto(t.Id, t.Subject, t.Category, t.Status, t.Priority, 
                    t.CreatedAt, t.User.Username, t.UserId, t.Replies.Count))
                .ToListAsync();
            return Results.Ok(tickets);
        });

        // Admin: update ticket status/priority
        group.MapPut("/{id:int}/update", async (int id, UpdateTicketRequest req, ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            var role = principal.FindFirstValue(ClaimTypes.Role);
            if (role != "Admin" && role != "Moderator") return Results.Forbid();

            var ticket = await db.SupportTickets.FirstOrDefaultAsync(t => t.Id == id);
            if (ticket is null) return Results.NotFound();

            if (req.Status is not null) ticket.Status = req.Status;
            if (req.Priority is not null) ticket.Priority = req.Priority;
            ticket.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(new { ticket.Status, ticket.Priority });
        });
    }
}

public record CreateTicketRequest(string Subject, string Message, string? Category, string? Priority);
public record TicketReplyRequest(string Message);
public record UpdateTicketRequest(string? Status, string? Priority);
public record TicketDto(int Id, string Subject, string Category, string Status, string Priority, DateTime CreatedAt, DateTime? UpdatedAt, int ReplyCount);
public record TicketDetailDto(int Id, string Subject, string Message, string Category, string Status, string Priority,
    DateTime CreatedAt, DateTime? UpdatedAt, string Username, List<TicketReplyDto> Replies);
public record TicketReplyDto(int Id, string Message, bool IsStaff, DateTime CreatedAt, string AuthorName, string? AuthorAvatarUrl);
public record TicketAdminDto(int Id, string Subject, string Category, string Status, string Priority, DateTime CreatedAt, string Username, int UserId, int ReplyCount);
