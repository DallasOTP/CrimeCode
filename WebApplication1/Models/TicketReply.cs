namespace CrimeCode.Models;

public class TicketReply
{
    public int Id { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool IsStaff { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int TicketId { get; set; }
    public SupportTicket Ticket { get; set; } = null!;
    public int AuthorId { get; set; }
    public User Author { get; set; } = null!;
}
