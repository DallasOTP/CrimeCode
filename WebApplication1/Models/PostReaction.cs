namespace CrimeCode.Models;

public class PostReaction
{
    public int Id { get; set; }
    public string Emoji { get; set; } = string.Empty; // e.g. "🔥", "👍", "😂"
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public int PostId { get; set; }
    public Post Post { get; set; } = null!;
}
