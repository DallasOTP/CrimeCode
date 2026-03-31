namespace CrimeCode.Models;

public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = "💬";
    public int SortOrder { get; set; }
    public int? ParentId { get; set; }
    public Category? Parent { get; set; }
    public bool IsMarketplace { get; set; } = false;

    public ICollection<Category> SubCategories { get; set; } = [];
    public ICollection<MarketplaceListing> MarketplaceListings { get; set; } = [];
}
