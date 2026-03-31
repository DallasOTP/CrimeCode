namespace CrimeCode.Models;

public class CreditTransaction
{
    public int Id { get; set; }
    public int Amount { get; set; }
    public string Type { get; set; } = string.Empty; // Earn, Spend, Transfer, Admin
    public string Reason { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public int? RelatedUserId { get; set; }
    public User? RelatedUser { get; set; }
}
