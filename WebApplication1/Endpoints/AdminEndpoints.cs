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

            var query = db.Users.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(u => u.Username.Contains(search) || u.Email.Contains(search));

            var total = await query.CountAsync();
            var users = await query
                .OrderByDescending(u => u.CreatedAt)
                .Skip((p - 1) * ps)
                .Take(ps)
                .Select(u => new AdminUserDto(u.Id, u.Username, u.Email, u.Role, u.CreatedAt, u.Credits, u.ReputationScore, u.IsBanned))
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

            var oldRole = user.Role;
            user.Role = req.Role;
            await db.SaveChangesAsync();

            var adminId = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            await AnalyticsEndpoints.LogAction(db, adminId, "ChangeRole", $"{user.Username}: {oldRole} → {req.Role}", "User", user.Id);

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

            // Remove user's related data
            var notificationsToRemove = await db.Notifications.Where(n => n.UserId == id || n.FromUserId == id).ToListAsync();
            db.Notifications.RemoveRange(notificationsToRemove);

            var messagesToRemove = await db.PrivateMessages.Where(m => m.SenderId == id || m.ReceiverId == id).ToListAsync();
            db.PrivateMessages.RemoveRange(messagesToRemove);

            db.Users.Remove(user);
            await db.SaveChangesAsync();

            await AnalyticsEndpoints.LogAction(db, currentUserId, "DeleteUser", $"Eliminato utente {user.Username} (ID:{user.Id})", "User", user.Id);

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
                totalListings = await db.MarketplaceListings.CountAsync(),
                totalOrders = await db.MarketplaceOrders.CountAsync(),
                totalSales = await db.MarketplaceOrders.CountAsync(o => o.Status == "Completed"),
                onlineUsers = await db.Users.CountAsync(u => u.LastSeenAt > cutoff),
                recentUsers = await db.Users.OrderByDescending(u => u.CreatedAt).Take(5)
                    .Select(u => new { u.Id, u.Username, u.CreatedAt }).ToListAsync(),
                recentListings = await db.MarketplaceListings.OrderByDescending(l => l.CreatedAt).Take(5)
                    .Include(l => l.Seller)
                    .Select(l => new { l.Id, l.Title, Seller = l.Seller.Username, l.CreatedAt }).ToListAsync()
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

            var adminIdBan = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            await AnalyticsEndpoints.LogAction(db, adminIdBan, "BanUser", $"Bannato {user.Username}: {req.Reason}", "User", user.Id);

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

            var adminIdUnban = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            await AnalyticsEndpoints.LogAction(db, adminIdUnban, "UnbanUser", $"Sbannato {user.Username}", "User", user.Id);

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

            var adminIdCredits = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            await AnalyticsEndpoints.LogAction(db, adminIdCredits, "ModifyCredits", $"{user.Username}: {(req.Amount >= 0 ? "+" : "")}{req.Amount} crediti ({req.Reason})", "User", user.Id);

            return Results.Ok(new { user.Id, user.Credits });
        });

        // --- Admin Marketplace --- 

        // Pending listings
        group.MapGet("/marketplace/pending", async (ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            if (!IsAdmin(principal) && !IsModerator(principal)) return Results.Forbid();
            var listings = await db.MarketplaceListings
                .Include(l => l.Seller).Include(l => l.Category)
                .Where(l => l.Status == "PendingApproval")
                .OrderBy(l => l.CreatedAt)
                .Select(l => new AdminListingDto(l.Id, l.Title, l.Seller.Username, l.SellerId, l.Status, l.Category.Name, l.PriceCrypto, l.Currency, l.CreatedAt, l.RejectionReason))
                .ToListAsync();
            return Results.Ok(listings);
        });

        // Review listing (approve/reject)
        group.MapPut("/marketplace/{id:int}/review", async (int id, AdminReviewListingRequest req, ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            if (!IsAdmin(principal) && !IsModerator(principal)) return Results.Forbid();
            var listing = await db.MarketplaceListings.FindAsync(id);
            if (listing is null) return Results.NotFound();

            if (req.Status == "Approved")
            {
                listing.Status = "Active";
                listing.RejectionReason = null;
            }
            else if (req.Status == "Rejected")
            {
                listing.Status = "Rejected";
                listing.RejectionReason = req.RejectionReason;
            }
            else return Results.BadRequest(new { error = "Status deve essere Approved o Rejected" });

            listing.UpdatedAt = DateTime.UtcNow;

            var adminIdReview = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            await AnalyticsEndpoints.LogAction(db, adminIdReview, "ReviewListing", $"Listing \"{listing.Title}\" → {listing.Status}", "Listing", listing.Id);

            await db.SaveChangesAsync();

            // Notify seller
            db.Notifications.Add(new Notification
            {
                UserId = listing.SellerId,
                Type = "listing_review",
                Message = req.Status == "Approved"
                    ? $"Il tuo annuncio \"{listing.Title}\" è stato approvato!"
                    : $"Il tuo annuncio \"{listing.Title}\" è stato rifiutato: {req.RejectionReason}"
            });
            await db.SaveChangesAsync();

            return Results.Ok(new { listing.Id, listing.Status });
        });

        // Vendor applications
        group.MapGet("/vendors/pending", async (ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            if (!IsAdmin(principal)) return Results.Forbid();
            var apps = await db.VendorApplications
                .Include(a => a.User)
                .Where(a => a.Status == "Pending")
                .OrderBy(a => a.CreatedAt)
                .Select(a => new VendorApplicationDto(a.Id, a.UserId, a.User.Username, a.User.AvatarUrl, a.TelegramUsername, a.Motivation, a.Specialization, a.Status, a.ReviewNote, a.CreatedAt, a.ReviewedAt))
                .ToListAsync();
            return Results.Ok(apps);
        });

        // All disputes
        group.MapGet("/disputes", async (ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            if (!IsAdmin(principal) && !IsModerator(principal)) return Results.Forbid();
            var disputes = await db.MarketplaceOrders
                .Include(o => o.Listing).Include(o => o.Buyer).Include(o => o.Seller)
                .Where(o => o.Status == "Disputed")
                .OrderBy(o => o.UpdatedAt)
                .Select(o => new AdminDisputeDto(o.Id, o.Listing.Title, o.Buyer.Username, o.Seller.Username, o.DisputeReason, o.Amount, o.Currency, o.Status, o.CreatedAt))
                .ToListAsync();
            return Results.Ok(disputes);
        });

        // Resolve dispute
        group.MapPut("/disputes/{id:int}/resolve", async (int id, AdminResolveDisputeRequest req, ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            if (!IsAdmin(principal)) return Results.Forbid();
            var order = await db.MarketplaceOrders.FirstOrDefaultAsync(o => o.Id == id);
            if (order is null) return Results.NotFound();
            if (order.Status != "Disputed") return Results.BadRequest(new { error = "Ordine non in disputa" });

            if (req.Resolution == "RefundBuyer")
            {
                order.EscrowStatus = "Refunded";
                order.Status = "Refunded";
                order.AdminNote = req.Note;
            }
            else if (req.Resolution == "ReleaseSeller")
            {
                order.EscrowStatus = "Released";
                order.ReleasedAt = DateTime.UtcNow;
                order.Status = "Completed";
                order.AdminNote = req.Note;
            }
            else return Results.BadRequest(new { error = "Resolution: RefundBuyer o ReleaseSeller" });

            order.UpdatedAt = DateTime.UtcNow;

            db.Notifications.Add(new Notification
            {
                UserId = order.BuyerId,
                Type = "dispute_resolved",
                Message = $"La disputa per l'ordine #{order.Id} è stata risolta: {req.Resolution}"
            });
            db.Notifications.Add(new Notification
            {
                UserId = order.SellerId,
                Type = "dispute_resolved",
                Message = $"La disputa per l'ordine #{order.Id} è stata risolta: {req.Resolution}"
            });

            var adminIdDispute = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            await AnalyticsEndpoints.LogAction(db, adminIdDispute, "ResolveDispute", $"Ordine #{order.Id} → {req.Resolution}", "Order", order.Id);

            await db.SaveChangesAsync();
            return Results.Ok(new { order.Id, order.Status, order.EscrowStatus });
        });

        // Auto-complete expired orders
        group.MapPost("/orders/auto-complete", async (ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            if (!IsAdmin(principal)) return Results.Forbid();
            var cutoff = DateTime.UtcNow.AddHours(-72);
            var toComplete = await db.MarketplaceOrders
                .Where(o => (o.Status == "Delivered" || o.Status == "Shipped") && o.UpdatedAt < cutoff && o.EscrowStatus == "Funded")
                .ToListAsync();

            foreach (var order in toComplete)
            {
                order.EscrowStatus = "Released";
                order.ReleasedAt = DateTime.UtcNow;
                order.Status = "Completed";
                order.UpdatedAt = DateTime.UtcNow;
            }

            await db.SaveChangesAsync();
            return Results.Ok(new { completedCount = toComplete.Count });
        });
    }

    private static bool IsAdmin(ClaimsPrincipal principal) =>
        principal.FindFirstValue(ClaimTypes.Role) == "Admin";

    private static bool IsModerator(ClaimsPrincipal principal) =>
        principal.FindFirstValue(ClaimTypes.Role) is "Moderator" or "Admin";
}
