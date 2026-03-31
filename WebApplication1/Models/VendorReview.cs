namespace CrimeCode.Models;

public class VendorReview
{
    public int Id { get; set; }
    public int Rating { get; set; } // 1-5
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int OrderId { get; set; }
    public MarketplaceOrder Order { get; set; } = null!;

    public int BuyerId { get; set; }
    public User Buyer { get; set; } = null!;

    public int SellerId { get; set; }
    public User Seller { get; set; } = null!;
}
