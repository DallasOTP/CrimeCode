namespace CrimeCode.Models;

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string? Bio { get; set; }
    public string? Signature { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
    public string Role { get; set; } = "Member"; // Member, Moderator, Admin
    public int Credits { get; set; } = 0;
    public int ReputationScore { get; set; } = 0;
    public string? CustomTitle { get; set; }
    public bool IsBanned { get; set; } = false;
    public string? BanReason { get; set; }
    public long? TelegramId { get; set; }
    public string Status { get; set; } = "online"; // online, away, busy, offline

    public ICollection<ForumThread> Threads { get; set; } = [];
    public ICollection<Post> Posts { get; set; } = [];
    public ICollection<PostLike> Likes { get; set; } = [];
    public ICollection<PostReaction> Reactions { get; set; } = [];
    public ICollection<Notification> Notifications { get; set; } = [];
    public ICollection<PrivateMessage> SentMessages { get; set; } = [];
    public ICollection<PrivateMessage> ReceivedMessages { get; set; } = [];
    public ICollection<Reputation> ReputationGiven { get; set; } = [];
    public ICollection<Reputation> ReputationReceived { get; set; } = [];
    public ICollection<CreditTransaction> CreditTransactions { get; set; } = [];
    public ICollection<MarketplaceListing> MarketplaceListings { get; set; } = [];
    public ICollection<ShoutboxMessage> ShoutboxMessages { get; set; } = [];
    public ICollection<UserFollow> Followers { get; set; } = [];
    public ICollection<UserFollow> Following { get; set; } = [];
    public ICollection<PostAttachment> Attachments { get; set; } = [];
    public ICollection<ChatMessage> SentChats { get; set; } = [];
    public ICollection<ChatMessage> ReceivedChats { get; set; } = [];
}
