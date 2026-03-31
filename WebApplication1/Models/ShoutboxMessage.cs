namespace CrimeCode.Models;

public class ShoutboxMessage
{
    public int Id { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int AuthorId { get; set; }
    public User Author { get; set; } = null!;
}
