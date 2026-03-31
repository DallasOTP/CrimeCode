using System.Security.Claims;
using CrimeCode.Data;
using CrimeCode.DTOs;
using CrimeCode.Models;
using Microsoft.EntityFrameworkCore;

namespace CrimeCode.Endpoints;

public static class MarketplaceEndpoints
{
    public static void MapMarketplaceEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/marketplace");

        // List marketplace items
        group.MapGet("/", async (int? categoryId, int? page, int? pageSize, string? type, CrimeCodeDbContext db) =>
        {
            var p = Math.Max(page ?? 1, 1);
            var ps = Math.Min(Math.Max(pageSize ?? 20, 1), 50);

            var query = db.MarketplaceListings
                .Include(ml => ml.Seller)
                .Include(ml => ml.Category)
                .Where(ml => ml.Status == "Active")
                .AsQueryable();

            if (categoryId.HasValue)
                query = query.Where(ml => ml.CategoryId == categoryId.Value);

            if (!string.IsNullOrWhiteSpace(type))
                query = query.Where(ml => ml.Type == type);

            var total = await query.CountAsync();
            var listings = await query
                .OrderByDescending(ml => ml.CreatedAt)
                .Skip((p - 1) * ps)
                .Take(ps)
                .Select(ml => new ListingDto(ml.Id, ml.Title, ml.Description, ml.Price, ml.Type, ml.Status,
                    ml.CreatedAt, ml.Seller.Username, ml.SellerId, ml.Seller.AvatarUrl, ml.Category.Name))
                .ToListAsync();

            return Results.Ok(new { listings, total, page = p, pageSize = ps });
        });

        // Get single listing
        group.MapGet("/{id:int}", async (int id, CrimeCodeDbContext db) =>
        {
            var ml = await db.MarketplaceListings
                .Include(m => m.Seller)
                .Include(m => m.Category)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (ml is null) return Results.NotFound();

            return Results.Ok(new ListingDto(ml.Id, ml.Title, ml.Description, ml.Price, ml.Type, ml.Status,
                ml.CreatedAt, ml.Seller.Username, ml.SellerId, ml.Seller.AvatarUrl, ml.Category.Name));
        });

        // Create listing
        group.MapPost("/", async (CreateListingRequest req, ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            var userId = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);

            if (string.IsNullOrWhiteSpace(req.Title) || req.Title.Length < 3)
                return Results.BadRequest(new { error = "Il titolo deve avere almeno 3 caratteri" });

            if (string.IsNullOrWhiteSpace(req.Description))
                return Results.BadRequest(new { error = "La descrizione non può essere vuota" });

            if (req.Price < 0)
                return Results.BadRequest(new { error = "Il prezzo non può essere negativo" });

            var validTypes = new[] { "Selling", "Buying", "Trading" };
            if (!validTypes.Contains(req.Type))
                return Results.BadRequest(new { error = "Tipo non valido" });

            var category = await db.Categories.FindAsync(req.CategoryId);
            if (category is null)
                return Results.BadRequest(new { error = "Categoria non trovata" });

            var listing = new MarketplaceListing
            {
                Title = req.Title,
                Description = req.Description,
                Price = req.Price,
                Type = req.Type,
                SellerId = userId,
                CategoryId = req.CategoryId
            };

            db.MarketplaceListings.Add(listing);
            await db.SaveChangesAsync();

            return Results.Created($"/api/marketplace/{listing.Id}", new { id = listing.Id });
        }).RequireAuthorization();

        // Update listing status
        group.MapPut("/{id:int}/status", async (int id, string status, ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            var userId = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var role = principal.FindFirstValue(ClaimTypes.Role);

            var listing = await db.MarketplaceListings.FindAsync(id);
            if (listing is null) return Results.NotFound();

            if (listing.SellerId != userId && role != "Admin" && role != "Moderator")
                return Results.Forbid();

            var validStatuses = new[] { "Active", "Sold", "Closed" };
            if (!validStatuses.Contains(status))
                return Results.BadRequest(new { error = "Stato non valido" });

            listing.Status = status;
            listing.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            return Results.Ok(new { listing.Id, listing.Status });
        }).RequireAuthorization();

        // Delete listing
        group.MapDelete("/{id:int}", async (int id, ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            var userId = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var role = principal.FindFirstValue(ClaimTypes.Role);

            var listing = await db.MarketplaceListings.FindAsync(id);
            if (listing is null) return Results.NotFound();

            if (listing.SellerId != userId && role != "Admin" && role != "Moderator")
                return Results.Forbid();

            db.MarketplaceListings.Remove(listing);
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization();
    }
}
