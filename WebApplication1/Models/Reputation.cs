namespace CrimeCode.Models;

public class Reputation
{
    public int Id { get; set; }
    public int Points { get; set; } = 1; // 1 = positive, -1 = negative
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int GiverId { get; set; }
    public User Giver { get; set; } = null!;

    public int ReceiverId { get; set; }
    public User Receiver { get; set; } = null!;
}
