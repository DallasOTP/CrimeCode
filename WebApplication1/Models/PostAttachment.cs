namespace CrimeCode.Models;

public class PostAttachment
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string StoredPath { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int PostId { get; set; }
    public Post Post { get; set; } = null!;

    public int UploaderId { get; set; }
    public User Uploader { get; set; } = null!;
}
