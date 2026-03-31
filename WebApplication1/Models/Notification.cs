namespace CrimeCode.Models;

public class Notification
{
    public int Id { get; set; }
    public string Type { get; set; } = string.Empty; // Reply, Like, Mention, System
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public int? FromUserId { get; set; }
    public User? FromUser { get; set; }

    public int? ThreadId { get; set; }
    public int? PostId { get; set; }
}
