namespace CrimeCode.Models;

public class Post
{
    public int Id { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EditedAt { get; set; }

    public int AuthorId { get; set; }
    public User Author { get; set; } = null!;

    public int ThreadId { get; set; }
    public ForumThread Thread { get; set; } = null!;

    public int? ParentPostId { get; set; }
    public Post? ParentPost { get; set; }

    public ICollection<Post> Replies { get; set; } = [];
    public ICollection<PostLike> Likes { get; set; } = [];
}
