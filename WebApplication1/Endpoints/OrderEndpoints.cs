using System.Security.Claims;
using CrimeCode.Data;
using CrimeCode.DTOs;
using CrimeCode.Models;
using Microsoft.EntityFrameworkCore;

namespace CrimeCode.Endpoints;

public static class OrderEndpoints
{
    public static void MapOrderEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/orders").RequireAuthorization();

        // Create order (buyer)
        group.MapPost("/", async (CreateOrderRequest req, ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            var buyerId = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var listing = await db.MarketplaceListings.Include(l => l.Seller).FirstOrDefaultAsync(l => l.Id == req.ListingId);
            if (listing is null) return Results.NotFound(new { error = "Annuncio non trovato" });
            if (listing.Status != "Active") return Results.BadRequest(new { error = "Annuncio non disponibile" });
            if (listing.SellerId == buyerId) return Results.BadRequest(new { error = "Non puoi acquistare il tuo annuncio" });
            if (req.Quantity < 1) return Results.BadRequest(new { error = "Quantità non valida" });
            if (listing.Stock < req.Quantity) return Results.BadRequest(new { error = $"Stock insufficiente ({listing.Stock} disponibili)" });

            if (listing.DeliveryType == "Shipping" && string.IsNullOrWhiteSpace(req.ShippingAddress))
                return Results.BadRequest(new { error = "Indirizzo di spedizione obbligatorio per prodotti fisici" });

            // Generate simulated escrow wallet
            var escrowWallet = GenerateEscrowAddress(listing.Currency);

            var order = new MarketplaceOrder
            {
                ListingId = listing.Id,
                BuyerId = buyerId,
                SellerId = listing.SellerId,
                Amount = listing.PriceCrypto * req.Quantity,
                Currency = listing.Currency,
                Quantity = req.Quantity,
                DeliveryType = listing.DeliveryType,
                ShippingAddress = req.ShippingAddress,
                EscrowWalletAddress = escrowWallet,
                Status = "Created",
                EscrowStatus = "Pending"
            };

            db.MarketplaceOrders.Add(order);

            // Notify seller
            db.Notifications.Add(new Notification
            {
                UserId = listing.SellerId,
                FromUserId = buyerId,
                Type = "new_order",
                Message = $"Nuovo ordine per \"{listing.Title}\" (x{req.Quantity})"
            });

            await db.SaveChangesAsync();

            return Results.Created($"/api/orders/{order.Id}", new
            {
                orderId = order.Id,
                escrowWallet = order.EscrowWalletAddress,
                amount = order.Amount,
                currency = order.Currency
            });
        });

        // Fund escrow (buyer confirms payment)
        group.MapPut("/{id:int}/fund", async (int id, FundEscrowRequest req, ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            var userId = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var order = await db.MarketplaceOrders.Include(o => o.Listing).FirstOrDefaultAsync(o => o.Id == id);
            if (order is null) return Results.NotFound();
            if (order.BuyerId != userId) return Results.Forbid();
            if (order.EscrowStatus != "Pending") return Results.BadRequest(new { error = "Escrow già finanziato" });

            if (string.IsNullOrWhiteSpace(req.BuyerTxId))
                return Results.BadRequest(new { error = "TX ID obbligatorio" });

            order.BuyerTxId = req.BuyerTxId;
            order.EscrowStatus = "Funded";
            order.FundedAt = DateTime.UtcNow;
            order.Status = "EscrowFunded";
            order.UpdatedAt = DateTime.UtcNow;

            // For instant digital delivery, auto-deliver
            if (order.DeliveryType == "Instant" && !string.IsNullOrWhiteSpace(order.Listing.DigitalContent))
            {
                order.DigitalDeliveryContent = order.Listing.DigitalContent;
                order.IsDelivered = true;
                order.Status = "Delivered";

                // Update stock
                order.Listing.Stock -= order.Quantity;
                order.Listing.SoldCount += order.Quantity;
                if (order.Listing.Stock <= 0) order.Listing.Status = "Sold";
            }

            // Notify seller
            db.Notifications.Add(new Notification
            {
                UserId = order.SellerId,
                FromUserId = userId,
                Type = "escrow_funded",
                Message = $"L'escrow per l'ordine #{order.Id} è stato finanziato"
            });

            await db.SaveChangesAsync();
            return Results.Ok(new { status = order.Status, escrowStatus = order.EscrowStatus, delivered = order.IsDelivered });
        });

        // Ship order (seller)
        group.MapPut("/{id:int}/ship", async (int id, ShipOrderRequest req, ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            var userId = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var order = await db.MarketplaceOrders.Include(o => o.Listing).FirstOrDefaultAsync(o => o.Id == id);
            if (order is null) return Results.NotFound();
            if (order.SellerId != userId) return Results.Forbid();
            if (order.EscrowStatus != "Funded") return Results.BadRequest(new { error = "Escrow non ancora finanziato" });
            if (order.DeliveryType != "Shipping") return Results.BadRequest(new { error = "Questo ordine non richiede spedizione" });

            order.TrackingNumber = req.TrackingNumber;
            order.Status = "Shipped";
            order.UpdatedAt = DateTime.UtcNow;

            order.Listing.Stock -= order.Quantity;
            order.Listing.SoldCount += order.Quantity;
            if (order.Listing.Stock <= 0) order.Listing.Status = "Sold";

            db.Notifications.Add(new Notification
            {
                UserId = order.BuyerId,
                FromUserId = userId,
                Type = "order_shipped",
                Message = $"L'ordine #{order.Id} è stato spedito. Tracking: {req.TrackingNumber}"
            });

            await db.SaveChangesAsync();
            return Results.Ok(new { status = order.Status, trackingNumber = order.TrackingNumber });
        });

        // Deliver manually (seller marks digital delivery)
        group.MapPut("/{id:int}/deliver", async (int id, string content, ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            var userId = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var order = await db.MarketplaceOrders.Include(o => o.Listing).FirstOrDefaultAsync(o => o.Id == id);
            if (order is null) return Results.NotFound();
            if (order.SellerId != userId) return Results.Forbid();
            if (order.EscrowStatus != "Funded") return Results.BadRequest(new { error = "Escrow non ancora finanziato" });

            order.DigitalDeliveryContent = content;
            order.IsDelivered = true;
            order.Status = "Delivered";
            order.UpdatedAt = DateTime.UtcNow;

            order.Listing.Stock -= order.Quantity;
            order.Listing.SoldCount += order.Quantity;
            if (order.Listing.Stock <= 0) order.Listing.Status = "Sold";

            db.Notifications.Add(new Notification
            {
                UserId = order.BuyerId,
                FromUserId = userId,
                Type = "order_delivered",
                Message = $"L'ordine #{order.Id} è stato consegnato"
            });

            await db.SaveChangesAsync();
            return Results.Ok(new { status = order.Status });
        });

        // Confirm delivery / Release escrow (buyer)
        group.MapPut("/{id:int}/confirm", async (int id, ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            var userId = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var order = await db.MarketplaceOrders.FirstOrDefaultAsync(o => o.Id == id);
            if (order is null) return Results.NotFound();
            if (order.BuyerId != userId) return Results.Forbid();
            if (order.Status != "Delivered" && order.Status != "Shipped")
                return Results.BadRequest(new { error = "L'ordine non è ancora stato consegnato" });

            order.EscrowStatus = "Released";
            order.ReleasedAt = DateTime.UtcNow;
            order.Status = "Completed";
            order.UpdatedAt = DateTime.UtcNow;

            db.Notifications.Add(new Notification
            {
                UserId = order.SellerId,
                FromUserId = userId,
                Type = "escrow_released",
                Message = $"L'escrow per l'ordine #{order.Id} è stato rilasciato. Pagamento completato!"
            });

            await db.SaveChangesAsync();
            return Results.Ok(new { status = order.Status, escrowStatus = order.EscrowStatus });
        });

        // Auto-confirm after 72h (buyer didn't dispute)
        // This would be a background job in production; here exposed as admin endpoint
        group.MapPost("/auto-complete", async (ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            var role = principal.FindFirstValue(ClaimTypes.Role);
            if (role != "Admin") return Results.Forbid();

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

        // Dispute order (buyer)
        group.MapPut("/{id:int}/dispute", async (int id, DisputeOrderRequest req, ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            var userId = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var order = await db.MarketplaceOrders.FirstOrDefaultAsync(o => o.Id == id);
            if (order is null) return Results.NotFound();
            if (order.BuyerId != userId) return Results.Forbid();
            if (order.EscrowStatus != "Funded")
                return Results.BadRequest(new { error = "Non puoi contestare questo ordine" });

            order.Status = "Disputed";
            order.EscrowStatus = "Disputed";
            order.DisputeReason = req.Reason;
            order.UpdatedAt = DateTime.UtcNow;

            db.Notifications.Add(new Notification
            {
                UserId = order.SellerId,
                FromUserId = userId,
                Type = "order_disputed",
                Message = $"L'ordine #{order.Id} è stato contestato dal compratore"
            });

            await db.SaveChangesAsync();
            return Results.Ok(new { status = order.Status });
        });

        // Resolve dispute (admin)
        group.MapPut("/{id:int}/resolve", async (int id, string resolution, string? note, ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            var role = principal.FindFirstValue(ClaimTypes.Role);
            if (role != "Admin") return Results.Forbid();

            var order = await db.MarketplaceOrders.FirstOrDefaultAsync(o => o.Id == id);
            if (order is null) return Results.NotFound();
            if (order.Status != "Disputed") return Results.BadRequest(new { error = "Ordine non in disputa" });

            if (resolution == "refund")
            {
                order.EscrowStatus = "Refunded";
                order.Status = "Refunded";
                order.AdminNote = note;
            }
            else if (resolution == "release")
            {
                order.EscrowStatus = "Released";
                order.ReleasedAt = DateTime.UtcNow;
                order.Status = "Completed";
                order.AdminNote = note;
            }
            else
                return Results.BadRequest(new { error = "Risoluzione non valida (refund/release)" });

            order.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(new { status = order.Status, escrowStatus = order.EscrowStatus });
        });

        // My orders (buyer)
        group.MapGet("/buying", async (ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            var userId = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var orders = await db.MarketplaceOrders
                .Include(o => o.Listing)
                .Include(o => o.Seller)
                .Where(o => o.BuyerId == userId)
                .OrderByDescending(o => o.CreatedAt)
                .Select(o => new OrderDto(o.Id, o.ListingId, o.Listing.Title, o.Listing.ImageUrl, o.BuyerId, "", o.SellerId, o.Seller.Username,
                    o.Amount, o.Currency, o.Quantity, o.Status, o.EscrowStatus, o.DeliveryType,
                    o.ShippingAddress, o.TrackingNumber, o.IsDelivered, o.EscrowWalletAddress,
                    o.IsDelivered ? o.DigitalDeliveryContent : null, o.DisputeReason, o.CreatedAt, o.UpdatedAt))
                .ToListAsync();

            return Results.Ok(orders);
        });

        // My sales (seller)
        group.MapGet("/selling", async (ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            var userId = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var orders = await db.MarketplaceOrders
                .Include(o => o.Listing)
                .Include(o => o.Buyer)
                .Where(o => o.SellerId == userId)
                .OrderByDescending(o => o.CreatedAt)
                .Select(o => new OrderDto(o.Id, o.ListingId, o.Listing.Title, o.Listing.ImageUrl, o.BuyerId, o.Buyer.Username, o.SellerId, "", 
                    o.Amount, o.Currency, o.Quantity, o.Status, o.EscrowStatus, o.DeliveryType,
                    o.ShippingAddress, o.TrackingNumber, o.IsDelivered, null,
                    null, o.DisputeReason, o.CreatedAt, o.UpdatedAt))
                .ToListAsync();

            return Results.Ok(orders);
        });

        // All disputed orders (admin)
        group.MapGet("/disputes", async (ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            var role = principal.FindFirstValue(ClaimTypes.Role);
            if (role != "Admin" && role != "Moderator") return Results.Forbid();

            var orders = await db.MarketplaceOrders
                .Include(o => o.Listing)
                .Include(o => o.Buyer)
                .Include(o => o.Seller)
                .Where(o => o.Status == "Disputed")
                .OrderBy(o => o.UpdatedAt)
                .Select(o => new OrderDto(o.Id, o.ListingId, o.Listing.Title, o.Listing.ImageUrl, o.BuyerId, o.Buyer.Username, o.SellerId, o.Seller.Username,
                    o.Amount, o.Currency, o.Quantity, o.Status, o.EscrowStatus, o.DeliveryType,
                    o.ShippingAddress, o.TrackingNumber, o.IsDelivered, null,
                    null, o.DisputeReason, o.CreatedAt, o.UpdatedAt))
                .ToListAsync();

            return Results.Ok(orders);
        });

        // Single order detail
        group.MapGet("/{id:int}", async (int id, ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            var userId = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var role = principal.FindFirstValue(ClaimTypes.Role);

            var o = await db.MarketplaceOrders
                .Include(o => o.Listing)
                .Include(o => o.Buyer)
                .Include(o => o.Seller)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (o is null) return Results.NotFound();
            if (o.BuyerId != userId && o.SellerId != userId && role != "Admin" && role != "Moderator")
                return Results.Forbid();

            var isBuyer = o.BuyerId == userId || role == "Admin";
            return Results.Ok(new OrderDto(o.Id, o.ListingId, o.Listing.Title, o.Listing.ImageUrl, o.BuyerId, o.Buyer.Username, o.SellerId, o.Seller.Username,
                o.Amount, o.Currency, o.Quantity, o.Status, o.EscrowStatus, o.DeliveryType,
                o.ShippingAddress, o.TrackingNumber, o.IsDelivered, o.EscrowWalletAddress,
                (isBuyer && o.IsDelivered) ? o.DigitalDeliveryContent : null, o.DisputeReason, o.CreatedAt, o.UpdatedAt));
        });
    }

    private static string GenerateEscrowAddress(string currency)
    {
        var random = new Random();
        var chars = "0123456789abcdef";
        var addr = new char[40];
        for (int i = 0; i < 40; i++)
            addr[i] = chars[random.Next(chars.Length)];

        return currency switch
        {
            "BTC" => "bc1q" + new string(addr, 0, 38),
            "ETH" => "0x" + new string(addr, 0, 40),
            "USDT" => "T" + new string(addr, 0, 33),
            "LTC" => "ltc1q" + new string(addr, 0, 36),
            "XMR" => "4" + new string(addr, 0, 40),
            _ => "escrow_" + new string(addr, 0, 20)
        };
    }
}
