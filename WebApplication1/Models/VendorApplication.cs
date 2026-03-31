namespace CrimeCode.Models;

public class VendorApplication
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public string TelegramUsername { get; set; } = string.Empty;
    public string Motivation { get; set; } = string.Empty; // why they want to be vendor
    public string? Specialization { get; set; } // what they sell

    public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected
    public string? ReviewNote { get; set; }
    public int? ReviewedById { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
