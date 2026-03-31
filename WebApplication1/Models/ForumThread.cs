namespace CrimeCode.Models;

public class ForumThread
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Prefix { get; set; } // [Tutorial], [Question], [Release], etc.
    public bool IsPinned { get; set; }
    public bool IsLocked { get; set; }
    public int ViewCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastActivityAt { get; set; }

    public int AuthorId { get; set; }
    public User Author { get; set; } = null!;

    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;

    public int? TagId { get; set; }
    public ThreadTag? Tag { get; set; }

    public ICollection<Post> Posts { get; set; } = [];
}
