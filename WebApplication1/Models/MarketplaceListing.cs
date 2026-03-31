namespace CrimeCode.Models;

public class MarketplaceListing
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal PriceCrypto { get; set; } // price in crypto (USD equivalent)
    public string Currency { get; set; } = "BTC"; // BTC, ETH, USDT, LTC, XMR
    public string Type { get; set; } = "Digital"; // Digital, Physical, Service
    public string Status { get; set; } = "PendingApproval"; // PendingApproval, Active, Sold, Closed, Rejected
    public string DeliveryType { get; set; } = "Instant"; // Instant, Manual, Shipping
    public string? DigitalContent { get; set; } // auto-delivered content for instant digital
    public string? ShippingInfo { get; set; } // shipping details for physical
    public string? ImageUrl { get; set; }
    public int Stock { get; set; } = 1;
    public int SoldCount { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public string? RejectionReason { get; set; }

    public int SellerId { get; set; }
    public User Seller { get; set; } = null!;

    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;

    public ICollection<MarketplaceOrder> Orders { get; set; } = [];
    public ICollection<ListingImage> Images { get; set; } = [];
    public ICollection<Wishlist> Wishlists { get; set; } = [];
    public ICollection<Voucher> Vouchers { get; set; } = [];
}
