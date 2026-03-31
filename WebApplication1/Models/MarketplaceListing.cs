namespace CrimeCode.Models;

public class MarketplaceListing
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Price { get; set; } // in credits
    public string Type { get; set; } = "Selling"; // Selling, Buying, Trading
    public string Status { get; set; } = "Active"; // Active, Sold, Closed
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public int SellerId { get; set; }
    public User Seller { get; set; } = null!;

    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;
}
