namespace CrimeCode.Models;

public class SupportTicket
{
    public int Id { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Category { get; set; } = "General"; // General, Order, Account, Bug, Other
    public string Status { get; set; } = "Open"; // Open, InProgress, Resolved, Closed
    public string Priority { get; set; } = "Normal"; // Low, Normal, High, Urgent
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public DateTime? ClosedAt { get; set; }

    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public int? AssignedToId { get; set; }
    public User? AssignedTo { get; set; }

    public ICollection<TicketReply> Replies { get; set; } = [];
}
