using System.Security.Claims;
using CrimeCode.Data;
using CrimeCode.DTOs;
using CrimeCode.Models;
using Microsoft.EntityFrameworkCore;

namespace CrimeCode.Endpoints;

public static class VendorEndpoints
{
    public static void MapVendorEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/vendor").RequireAuthorization();

        // Apply to become vendor
        group.MapPost("/apply", async (VendorApplicationRequest req, ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            var userId = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var user = await db.Users.FindAsync(userId);
            if (user is null) return Results.NotFound();

            if (user.IsVendor)
                return Results.BadRequest(new { error = "Sei già un venditore verificato" });

            var existing = await db.VendorApplications
                .Where(va => va.UserId == userId && va.Status == "Pending")
                .FirstOrDefaultAsync();
            if (existing is not null)
                return Results.BadRequest(new { error = "Hai già una richiesta in sospeso" });

            if (string.IsNullOrWhiteSpace(req.TelegramUsername))
                return Results.BadRequest(new { error = "Username Telegram obbligatorio" });
            if (string.IsNullOrWhiteSpace(req.Motivation))
                return Results.BadRequest(new { error = "Motivazione obbligatoria" });

            var application = new VendorApplication
            {
                UserId = userId,
                TelegramUsername = req.TelegramUsername,
                Motivation = req.Motivation,
                Specialization = req.Specialization
            };

            db.VendorApplications.Add(application);
            await db.SaveChangesAsync();

            return Results.Created($"/api/vendor/application/{application.Id}", new { id = application.Id });
        });

        // My vendor status
        group.MapGet("/status", async (ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            var userId = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var user = await db.Users.FindAsync(userId);
            if (user is null) return Results.NotFound();

            var lastApp = await db.VendorApplications
                .Where(va => va.UserId == userId)
                .OrderByDescending(va => va.CreatedAt)
                .FirstOrDefaultAsync();

            return Results.Ok(new
            {
                isVendor = user.IsVendor,
                vendorSince = user.VendorApprovedAt,
                vendorBio = user.VendorBio,
                application = lastApp is null ? null : new VendorApplicationDto(
                    lastApp.Id, lastApp.UserId, user.Username, user.AvatarUrl,
                    lastApp.TelegramUsername, lastApp.Motivation, lastApp.Specialization,
                    lastApp.Status, lastApp.ReviewNote, lastApp.CreatedAt, lastApp.ReviewedAt)
            });
        });

        // Update vendor bio
        group.MapPut("/bio", async (string bio, ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            var userId = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var user = await db.Users.FindAsync(userId);
            if (user is null) return Results.NotFound();
            if (!user.IsVendor) return Results.Forbid();

            user.VendorBio = bio;
            await db.SaveChangesAsync();
            return Results.Ok(new { vendorBio = user.VendorBio });
        });

        // List all applications (admin/mod)
        group.MapGet("/applications", async (string? status, ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            var role = principal.FindFirstValue(ClaimTypes.Role);
            if (role != "Admin" && role != "Moderator") return Results.Forbid();

            var query = db.VendorApplications
                .Include(va => va.User)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(va => va.Status == status);
            else
                query = query.Where(va => va.Status == "Pending");

            var apps = await query
                .OrderBy(va => va.CreatedAt)
                .Select(va => new VendorApplicationDto(
                    va.Id, va.UserId, va.User.Username, va.User.AvatarUrl,
                    va.TelegramUsername, va.Motivation, va.Specialization,
                    va.Status, va.ReviewNote, va.CreatedAt, va.ReviewedAt))
                .ToListAsync();

            return Results.Ok(apps);
        });

        // Review application (admin/mod)
        group.MapPut("/applications/{id:int}/review", async (int id, VendorReviewRequest req, ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            var role = principal.FindFirstValue(ClaimTypes.Role);
            if (role != "Admin" && role != "Moderator") return Results.Forbid();

            var adminId = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var application = await db.VendorApplications.Include(va => va.User).FirstOrDefaultAsync(va => va.Id == id);
            if (application is null) return Results.NotFound();

            if (application.Status != "Pending")
                return Results.BadRequest(new { error = "Questa richiesta è già stata gestita" });

            if (req.Status != "Approved" && req.Status != "Rejected")
                return Results.BadRequest(new { error = "Status deve essere Approved o Rejected" });

            application.Status = req.Status;
            application.ReviewNote = req.ReviewNote;
            application.ReviewedById = adminId;
            application.ReviewedAt = DateTime.UtcNow;

            if (req.Status == "Approved")
            {
                application.User.IsVendor = true;
                application.User.VendorApprovedAt = DateTime.UtcNow;

                // Notify user
                db.Notifications.Add(new Notification
                {
                    UserId = application.UserId,
                    FromUserId = adminId,
                    Type = "vendor_approved",
                    Message = "La tua richiesta venditore è stata approvata! Ora puoi pubblicare annunci.",
                });
            }
            else
            {
                db.Notifications.Add(new Notification
                {
                    UserId = application.UserId,
                    FromUserId = adminId,
                    Type = "vendor_rejected",
                    Message = $"La tua richiesta venditore è stata rifiutata. Motivo: {req.ReviewNote ?? "N/A"}",
                });
            }

            await db.SaveChangesAsync();
            return Results.Ok(new { id = application.Id, status = application.Status });
        });

        // List all vendors (public)
        app.MapGet("/api/vendors", async (CrimeCodeDbContext db) =>
        {
            var vendors = await db.Users
                .Where(u => u.IsVendor)
                .Select(u => new VendorProfileDto(
                    u.Id, u.Username, u.AvatarUrl, u.VendorBio, u.VendorApprovedAt,
                    u.ReputationScore,
                    db.MarketplaceOrders.Count(o => o.SellerId == u.Id && o.Status == "Completed"),
                    db.MarketplaceListings.Count(l => l.SellerId == u.Id && l.Status == "Active"),
                    db.MarketplaceOrders.Where(o => o.SellerId == u.Id && o.Status == "Completed").Sum(o => o.Amount)
                ))
                .ToListAsync();

            return Results.Ok(vendors);
        });
    }
}
