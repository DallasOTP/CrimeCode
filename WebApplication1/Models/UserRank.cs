namespace CrimeCode.Models;

public class UserRank
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#00e5a0"; // CSS color
    public string Icon { get; set; } = "⭐";
    public int MinPosts { get; set; }
    public int MinReputation { get; set; }
    public int SortOrder { get; set; }
}
