namespace CrimeCode.Models;

public class ListingImage
{
    public int Id { get; set; }
    public string Url { get; set; } = string.Empty;
    public int SortOrder { get; set; } = 0;

    public int ListingId { get; set; }
    public MarketplaceListing Listing { get; set; } = null!;
}
