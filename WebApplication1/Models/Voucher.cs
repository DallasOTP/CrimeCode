namespace CrimeCode.Models;

public class Voucher
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public decimal DiscountPercent { get; set; } // 0-100
    public decimal? MaxDiscount { get; set; } // max discount in crypto
    public int MaxUses { get; set; } = 1;
    public int UsedCount { get; set; } = 0;
    public bool IsActive { get; set; } = true;
    public DateTime? ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int SellerId { get; set; }
    public User Seller { get; set; } = null!;

    // Optional: limit to specific listing
    public int? ListingId { get; set; }
    public MarketplaceListing? Listing { get; set; }
}
