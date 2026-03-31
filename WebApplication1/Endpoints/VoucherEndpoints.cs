using System.Security.Claims;
using CrimeCode.Data;
using CrimeCode.DTOs;
using CrimeCode.Models;
using Microsoft.EntityFrameworkCore;

namespace CrimeCode.Endpoints;

public static class VoucherEndpoints
{
    public static void MapVoucherEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/vouchers").RequireAuthorization();

        // Create voucher (vendor only)
        group.MapPost("/", async (CreateVoucherRequest req, ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            var userId = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var user = await db.Users.FindAsync(userId);
            if (user is null) return Results.NotFound();
            if (!user.IsVendor) return Results.Json(new { error = "Solo i venditori possono creare voucher" }, statusCode: 403);

            if (string.IsNullOrWhiteSpace(req.Code) || req.Code.Length < 3)
                return Results.BadRequest(new { error = "Il codice deve avere almeno 3 caratteri" });
            if (req.DiscountPercent <= 0 || req.DiscountPercent > 100)
                return Results.BadRequest(new { error = "Sconto deve essere tra 1 e 100%" });
            if (req.MaxUses < 1)
                return Results.BadRequest(new { error = "Usi massimi deve essere almeno 1" });

            var codeUpper = req.Code.ToUpperInvariant();
            var exists = await db.Vouchers.AnyAsync(v => v.Code == codeUpper);
            if (exists) return Results.BadRequest(new { error = "Codice già in uso" });

            if (req.ListingId.HasValue)
            {
                var listing = await db.MarketplaceListings.FindAsync(req.ListingId.Value);
                if (listing is null || listing.SellerId != userId)
                    return Results.BadRequest(new { error = "Annuncio non trovato o non tuo" });
            }

            var voucher = new Voucher
            {
                Code = codeUpper,
                DiscountPercent = req.DiscountPercent,
                MaxDiscount = req.MaxDiscount,
                MaxUses = req.MaxUses,
                ExpiresAt = req.ExpiresAt,
                SellerId = userId,
                ListingId = req.ListingId,
                IsActive = true
            };

            db.Vouchers.Add(voucher);
            await db.SaveChangesAsync();
            return Results.Created($"/api/vouchers/{voucher.Id}", new { id = voucher.Id, code = voucher.Code });
        });

        // My vouchers (vendor)
        group.MapGet("/my", async (ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            var userId = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var vouchers = await db.Vouchers
                .Include(v => v.Listing)
                .Where(v => v.SellerId == userId)
                .OrderByDescending(v => v.CreatedAt)
                .Select(v => new VoucherDto(v.Id, v.Code, v.DiscountPercent, v.MaxDiscount, v.MaxUses, v.UsedCount, v.IsActive, v.ExpiresAt,
                    v.SellerId, "", v.ListingId, v.Listing != null ? v.Listing.Title : null))
                .ToListAsync();

            return Results.Ok(vouchers);
        });

        // Check / apply voucher (buyer, before order)
        group.MapPost("/check", async (ApplyVoucherRequest req, ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            var code = req.Code.ToUpperInvariant();
            var voucher = await db.Vouchers.FirstOrDefaultAsync(v => v.Code == code);

            if (voucher is null)
                return Results.Ok(new VoucherCheckResult(false, 0, null, "Codice non trovato"));
            if (!voucher.IsActive)
                return Results.Ok(new VoucherCheckResult(false, 0, null, "Voucher non attivo"));
            if (voucher.UsedCount >= voucher.MaxUses)
                return Results.Ok(new VoucherCheckResult(false, 0, null, "Voucher esaurito"));
            if (voucher.ExpiresAt.HasValue && voucher.ExpiresAt < DateTime.UtcNow)
                return Results.Ok(new VoucherCheckResult(false, 0, null, "Voucher scaduto"));
            if (voucher.ListingId.HasValue && voucher.ListingId != req.ListingId)
                return Results.Ok(new VoucherCheckResult(false, 0, null, "Voucher non valido per questo annuncio"));

            return Results.Ok(new VoucherCheckResult(true, voucher.DiscountPercent, voucher.MaxDiscount, null));
        });

        // Toggle voucher active/inactive
        group.MapPut("/{id:int}/toggle", async (int id, ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            var userId = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var voucher = await db.Vouchers.FindAsync(id);
            if (voucher is null) return Results.NotFound();
            if (voucher.SellerId != userId) return Results.Forbid();

            voucher.IsActive = !voucher.IsActive;
            await db.SaveChangesAsync();
            return Results.Ok(new { id = voucher.Id, isActive = voucher.IsActive });
        });

        // Delete voucher
        group.MapDelete("/{id:int}", async (int id, ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            var userId = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var role = principal.FindFirstValue(ClaimTypes.Role);
            var voucher = await db.Vouchers.FindAsync(id);
            if (voucher is null) return Results.NotFound();
            if (voucher.SellerId != userId && role != "Admin") return Results.Forbid();

            db.Vouchers.Remove(voucher);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }
}
