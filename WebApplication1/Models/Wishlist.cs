namespace CrimeCode.Models;

public class Wishlist
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public int ListingId { get; set; }
    public MarketplaceListing Listing { get; set; } = null!;
}
