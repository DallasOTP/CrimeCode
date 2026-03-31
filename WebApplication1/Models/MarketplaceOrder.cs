namespace CrimeCode.Models;

public class MarketplaceOrder
{
    public int Id { get; set; }
    public int ListingId { get; set; }
    public MarketplaceListing Listing { get; set; } = null!;
    public int BuyerId { get; set; }
    public User Buyer { get; set; } = null!;
    public int SellerId { get; set; }
    public User Seller { get; set; } = null!;

    public decimal Amount { get; set; }
    public string Currency { get; set; } = "BTC";
    public int Quantity { get; set; } = 1;

    // Escrow
    public string EscrowStatus { get; set; } = "Pending"; // Pending, Funded, Released, Refunded, Disputed
    public string? EscrowWalletAddress { get; set; } // simulated escrow address
    public string? BuyerTxId { get; set; } // buyer's tx proof
    public DateTime? FundedAt { get; set; }
    public DateTime? ReleasedAt { get; set; }

    // Delivery
    public string DeliveryType { get; set; } = "Instant"; // Instant, Manual, Shipping
    public string? DigitalDeliveryContent { get; set; } // revealed after escrow release
    public string? ShippingAddress { get; set; }
    public string? TrackingNumber { get; set; }
    public bool IsDelivered { get; set; } = false;

    // Status
    public string Status { get; set; } = "Created"; // Created, EscrowFunded, Processing, Shipped, Delivered, Completed, Disputed, Cancelled, Refunded
    public string? DisputeReason { get; set; }
    public string? AdminNote { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
