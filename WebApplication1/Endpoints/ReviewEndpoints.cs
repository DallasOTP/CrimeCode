using System.Security.Claims;
using CrimeCode.Data;
using CrimeCode.DTOs;
using CrimeCode.Models;
using Microsoft.EntityFrameworkCore;

namespace CrimeCode.Endpoints;

public static class ReviewEndpoints
{
    public static void MapReviewEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/reviews").RequireAuthorization().RequireRateLimiting("api-write");

        // Create review (buyer, after order completed)
        group.MapPost("/", async (CreateReviewRequest req, ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            var userId = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);

            if (req.Rating < 1 || req.Rating > 5)
                return Results.BadRequest(new { error = "Il rating deve essere tra 1 e 5" });

            var order = await db.MarketplaceOrders.Include(o => o.Listing).FirstOrDefaultAsync(o => o.Id == req.OrderId);
            if (order is null) return Results.NotFound(new { error = "Ordine non trovato" });
            if (order.BuyerId != userId) return Results.Forbid();
            if (order.Status != "Completed")
                return Results.BadRequest(new { error = "Puoi recensire solo ordini completati" });

            var existing = await db.VendorReviews.AnyAsync(r => r.OrderId == req.OrderId);
            if (existing) return Results.BadRequest(new { error = "Hai già recensito questo ordine" });

            var review = new VendorReview
            {
                OrderId = req.OrderId,
                Rating = req.Rating,
                Comment = req.Comment,
                BuyerId = userId,
                SellerId = order.SellerId
            };

            db.VendorReviews.Add(review);

            db.Notifications.Add(new Notification
            {
                UserId = order.SellerId,
                FromUserId = userId,
                Type = "new_review",
                Message = $"Hai ricevuto una recensione di {req.Rating} stelle per l'ordine #{order.Id}"
            });

            await db.SaveChangesAsync();
            return Results.Created($"/api/reviews/{review.Id}", new { id = review.Id });
        });

        // Get reviews for a seller
        group.MapGet("/seller/{sellerId:int}", async (int sellerId, CrimeCodeDbContext db) =>
        {
            var reviews = await db.VendorReviews
                .Include(r => r.Buyer)
                .Where(r => r.SellerId == sellerId)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new VendorReviewDto(r.Id, r.OrderId, r.Rating, r.Comment, r.CreatedAt,
                    r.BuyerId, r.Buyer.Username, r.Buyer.AvatarUrl, r.SellerId, ""))
                .ToListAsync();

            var avg = reviews.Count > 0 ? reviews.Average(r => r.Rating) : 0;
            return Results.Ok(new { reviews, averageRating = Math.Round(avg, 1), totalReviews = reviews.Count });
        }).AllowAnonymous();

        // Get reviews for a listing (all completed orders for that listing)
        group.MapGet("/listing/{listingId:int}", async (int listingId, CrimeCodeDbContext db) =>
        {
            var reviews = await db.VendorReviews
                .Include(r => r.Buyer)
                .Include(r => r.Order)
                .Where(r => r.Order.ListingId == listingId)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new VendorReviewDto(r.Id, r.OrderId, r.Rating, r.Comment, r.CreatedAt,
                    r.BuyerId, r.Buyer.Username, r.Buyer.AvatarUrl, r.SellerId, ""))
                .ToListAsync();

            var avg = reviews.Count > 0 ? reviews.Average(r => r.Rating) : 0;
            return Results.Ok(new { reviews, averageRating = Math.Round(avg, 1), totalReviews = reviews.Count });
        }).AllowAnonymous();

        // Check if buyer can review an order
        group.MapGet("/can-review/{orderId:int}", async (int orderId, ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            var userId = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var order = await db.MarketplaceOrders.FirstOrDefaultAsync(o => o.Id == orderId);
            if (order is null) return Results.NotFound();

            var canReview = order.BuyerId == userId && order.Status == "Completed" 
                && !await db.VendorReviews.AnyAsync(r => r.OrderId == orderId);

            return Results.Ok(new { canReview });
        });
    }
}
