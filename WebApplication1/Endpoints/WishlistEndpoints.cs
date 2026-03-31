using System.Security.Claims;
using CrimeCode.Data;
using CrimeCode.DTOs;
using CrimeCode.Models;
using Microsoft.EntityFrameworkCore;

namespace CrimeCode.Endpoints;

public static class WishlistEndpoints
{
    public static void MapWishlistEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/wishlist").RequireAuthorization();

        // Get my wishlist
        group.MapGet("/", async (ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            var userId = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var items = await db.Wishlists
                .Include(w => w.Listing).ThenInclude(l => l.Seller)
                .Where(w => w.UserId == userId)
                .OrderByDescending(w => w.CreatedAt)
                .Select(w => new WishlistDto(w.Id, w.ListingId, w.Listing.Title, w.Listing.ImageUrl,
                    w.Listing.PriceCrypto, w.Listing.Currency, w.Listing.Seller.Username, w.CreatedAt))
                .ToListAsync();

            return Results.Ok(items);
        });

        // Toggle wishlist (add/remove)
        group.MapPost("/toggle", async (ToggleWishlistRequest req, ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            var userId = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var listing = await db.MarketplaceListings.FindAsync(req.ListingId);
            if (listing is null) return Results.NotFound(new { error = "Annuncio non trovato" });

            var existing = await db.Wishlists.FirstOrDefaultAsync(w => w.UserId == userId && w.ListingId == req.ListingId);
            if (existing is not null)
            {
                db.Wishlists.Remove(existing);
                await db.SaveChangesAsync();
                return Results.Ok(new { inWishlist = false });
            }

            db.Wishlists.Add(new Wishlist { UserId = userId, ListingId = req.ListingId });
            await db.SaveChangesAsync();
            return Results.Ok(new { inWishlist = true });
        });

        // Check if listing is in wishlist
        group.MapGet("/check/{listingId:int}", async (int listingId, ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            var userId = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var exists = await db.Wishlists.AnyAsync(w => w.UserId == userId && w.ListingId == listingId);
            return Results.Ok(new { inWishlist = exists });
        });
    }
}
