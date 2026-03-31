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

        // List active marketplace items (public)
        group.MapGet("/", async (int? categoryId, int? page, int? pageSize, string? type, string? search, string? currency, CrimeCodeDbContext db) =>
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

            if (!string.IsNullOrWhiteSpace(currency))
                query = query.Where(ml => ml.Currency == currency);

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(ml => ml.Title.Contains(search) || ml.Description.Contains(search));

            var total = await query.CountAsync();
            var listings = await query
                .OrderByDescending(ml => ml.CreatedAt)
                .Skip((p - 1) * ps)
                .Take(ps)
                .Select(ml => new ListingDto(ml.Id, ml.Title, ml.Description, ml.PriceCrypto, ml.Currency, ml.Type, ml.Status, ml.DeliveryType,
                    ml.CreatedAt, ml.Seller.Username, ml.SellerId, ml.Seller.AvatarUrl, ml.Seller.IsVendor, ml.Category.Name, ml.CategoryId,
                    ml.ImageUrl, ml.Stock, ml.SoldCount, null, ml.ShippingInfo))
                .ToListAsync();

            return Results.Ok(new { listings, total, page = p, pageSize = ps });
        });

        // Featured / stats
        group.MapGet("/stats", async (CrimeCodeDbContext db) =>
        {
            var totalListings = await db.MarketplaceListings.CountAsync(l => l.Status == "Active");
            var totalVendors = await db.Users.CountAsync(u => u.IsVendor);
            var totalSold = await db.MarketplaceListings.SumAsync(l => l.SoldCount);
            var totalOrders = await db.MarketplaceOrders.CountAsync(o => o.Status == "Completed");
            return Results.Ok(new { totalListings, totalVendors, totalSold, totalOrders });
        });

        // Get single listing detail
        group.MapGet("/{id:int}", async (int id, CrimeCodeDbContext db) =>
        {
            var ml = await db.MarketplaceListings
                .Include(m => m.Seller)
                .Include(m => m.Category)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (ml is null) return Results.NotFound();

            var sellerSales = await db.MarketplaceOrders.CountAsync(o => o.SellerId == ml.SellerId && o.Status == "Completed");

            return Results.Ok(new ListingDetailDto(ml.Id, ml.Title, ml.Description, ml.PriceCrypto, ml.Currency, ml.Type, ml.Status, ml.DeliveryType,
                ml.CreatedAt, ml.Seller.Username, ml.SellerId, ml.Seller.AvatarUrl, ml.Seller.IsVendor, ml.Category.Name, ml.CategoryId,
                ml.ImageUrl, ml.Stock, ml.SoldCount, ml.ShippingInfo, ml.Seller.ReputationScore, sellerSales, ml.Seller.VendorBio));
        });

        // Create listing (vendors only)
        group.MapPost("/", async (CreateListingRequest req, ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            var userId = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var user = await db.Users.FindAsync(userId);
            var role = principal.FindFirstValue(ClaimTypes.Role);

            if (user is null) return Results.NotFound();
            if (!user.IsVendor && role != "Admin" && role != "Moderator")
                return Results.Json(new { error = "Devi essere un venditore verificato per pubblicare annunci" }, statusCode: 403);

            if (string.IsNullOrWhiteSpace(req.Title) || req.Title.Length < 3)
                return Results.BadRequest(new { error = "Il titolo deve avere almeno 3 caratteri" });
            if (string.IsNullOrWhiteSpace(req.Description))
                return Results.BadRequest(new { error = "La descrizione non può essere vuota" });
            if (req.PriceCrypto <= 0)
                return Results.BadRequest(new { error = "Il prezzo deve essere maggiore di 0" });

            var validTypes = new[] { "Digital", "Physical", "Service" };
            if (!validTypes.Contains(req.Type))
                return Results.BadRequest(new { error = "Tipo non valido (Digital/Physical/Service)" });

            var validCurrencies = new[] { "BTC", "ETH", "USDT", "LTC", "XMR" };
            if (!validCurrencies.Contains(req.Currency))
                return Results.BadRequest(new { error = "Valuta non valida" });

            var validDelivery = new[] { "Instant", "Manual", "Shipping" };
            if (!validDelivery.Contains(req.DeliveryType))
                return Results.BadRequest(new { error = "Tipo di consegna non valido" });

            if (req.DeliveryType == "Instant" && string.IsNullOrWhiteSpace(req.DigitalContent))
                return Results.BadRequest(new { error = "Il contenuto digitale è obbligatorio per consegna istantanea" });

            var category = await db.Categories.FindAsync(req.CategoryId);
            if (category is null)
                return Results.BadRequest(new { error = "Categoria non trovata" });

            var listing = new MarketplaceListing
            {
                Title = req.Title,
                Description = req.Description,
                PriceCrypto = req.PriceCrypto,
                Currency = req.Currency,
                Type = req.Type,
                DeliveryType = req.DeliveryType,
                DigitalContent = req.DigitalContent,
                ShippingInfo = req.ShippingInfo,
                ImageUrl = req.ImageUrl,
                Stock = Math.Max(req.Stock, 1),
                SellerId = userId,
                CategoryId = req.CategoryId,
                Status = (role == "Admin" || role == "Moderator") ? "Active" : "PendingApproval"
            };

            db.MarketplaceListings.Add(listing);
            await db.SaveChangesAsync();

            return Results.Created($"/api/marketplace/{listing.Id}", new { id = listing.Id, status = listing.Status });
        }).RequireAuthorization();

        // Edit listing (owner or admin)
        group.MapPut("/{id:int}", async (int id, CreateListingRequest req, ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            var userId = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var role = principal.FindFirstValue(ClaimTypes.Role);

            var listing = await db.MarketplaceListings.FindAsync(id);
            if (listing is null) return Results.NotFound();

            if (listing.SellerId != userId && role != "Admin" && role != "Moderator")
                return Results.Forbid();

            listing.Title = req.Title;
            listing.Description = req.Description;
            listing.PriceCrypto = req.PriceCrypto;
            listing.Currency = req.Currency;
            listing.Type = req.Type;
            listing.DeliveryType = req.DeliveryType;
            listing.DigitalContent = req.DigitalContent;
            listing.ShippingInfo = req.ShippingInfo;
            listing.ImageUrl = req.ImageUrl;
            listing.Stock = Math.Max(req.Stock, 1);
            listing.CategoryId = req.CategoryId;
            listing.UpdatedAt = DateTime.UtcNow;

            // Re-queue for approval if edited by vendor
            if (role != "Admin" && role != "Moderator")
                listing.Status = "PendingApproval";

            await db.SaveChangesAsync();
            return Results.Ok(new { id = listing.Id, status = listing.Status });
        }).RequireAuthorization();

        // Approve / reject listing (admin/mod only)
        group.MapPut("/{id:int}/review", async (int id, VendorReviewRequest req, ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            var role = principal.FindFirstValue(ClaimTypes.Role);
            if (role != "Admin" && role != "Moderator")
                return Results.Forbid();

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
                listing.RejectionReason = req.ReviewNote;
            }
            else
                return Results.BadRequest(new { error = "Status deve essere Approved o Rejected" });

            listing.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(new { id = listing.Id, status = listing.Status });
        }).RequireAuthorization();

        // Get pending listings (admin/mod)
        group.MapGet("/pending", async (ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            var role = principal.FindFirstValue(ClaimTypes.Role);
            if (role != "Admin" && role != "Moderator")
                return Results.Forbid();

            var listings = await db.MarketplaceListings
                .Include(ml => ml.Seller)
                .Include(ml => ml.Category)
                .Where(ml => ml.Status == "PendingApproval")
                .OrderBy(ml => ml.CreatedAt)
                .Select(ml => new ListingDto(ml.Id, ml.Title, ml.Description, ml.PriceCrypto, ml.Currency, ml.Type, ml.Status, ml.DeliveryType,
                    ml.CreatedAt, ml.Seller.Username, ml.SellerId, ml.Seller.AvatarUrl, ml.Seller.IsVendor, ml.Category.Name, ml.CategoryId,
                    ml.ImageUrl, ml.Stock, ml.SoldCount, null, ml.ShippingInfo))
                .ToListAsync();

            return Results.Ok(listings);
        }).RequireAuthorization();

        // My listings (vendor)
        group.MapGet("/my", async (ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            var userId = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var listings = await db.MarketplaceListings
                .Include(ml => ml.Seller)
                .Include(ml => ml.Category)
                .Where(ml => ml.SellerId == userId)
                .OrderByDescending(ml => ml.CreatedAt)
                .Select(ml => new ListingDto(ml.Id, ml.Title, ml.Description, ml.PriceCrypto, ml.Currency, ml.Type, ml.Status, ml.DeliveryType,
                    ml.CreatedAt, ml.Seller.Username, ml.SellerId, ml.Seller.AvatarUrl, ml.Seller.IsVendor, ml.Category.Name, ml.CategoryId,
                    ml.ImageUrl, ml.Stock, ml.SoldCount, ml.RejectionReason, ml.ShippingInfo))
                .ToListAsync();

            return Results.Ok(listings);
        }).RequireAuthorization();

        // Update listing status (close/reactivate)
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

        // Vendor listings by seller ID
        group.MapGet("/vendor/{sellerId:int}", async (int sellerId, CrimeCodeDbContext db) =>
        {
            var seller = await db.Users.FindAsync(sellerId);
            if (seller is null) return Results.NotFound();

            var listings = await db.MarketplaceListings
                .Include(ml => ml.Category)
                .Where(ml => ml.SellerId == sellerId && ml.Status == "Active")
                .OrderByDescending(ml => ml.CreatedAt)
                .Select(ml => new ListingDto(ml.Id, ml.Title, ml.Description, ml.PriceCrypto, ml.Currency, ml.Type, ml.Status, ml.DeliveryType,
                    ml.CreatedAt, seller.Username, sellerId, seller.AvatarUrl, seller.IsVendor, ml.Category.Name, ml.CategoryId,
                    ml.ImageUrl, ml.Stock, ml.SoldCount, null, ml.ShippingInfo))
                .ToListAsync();

            var profile = new VendorProfileDto(
                seller.Id, seller.Username, seller.AvatarUrl, seller.VendorBio, seller.VendorApprovedAt,
                seller.ReputationScore,
                await db.MarketplaceOrders.CountAsync(o => o.SellerId == sellerId && o.Status == "Completed"),
                listings.Count,
                await db.MarketplaceOrders.Where(o => o.SellerId == sellerId && o.Status == "Completed").SumAsync(o => o.Amount)
            );

            return Results.Ok(new { vendor = profile, listings });
        });
    }
}
