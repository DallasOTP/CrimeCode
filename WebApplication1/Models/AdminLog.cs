namespace CrimeCode.Models;

public class AdminLog
{
    public int Id { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public string? TargetType { get; set; }
    public int? TargetId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int AdminId { get; set; }
    public User Admin { get; set; } = null!;
}
