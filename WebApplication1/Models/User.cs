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
    public bool IsVendor { get; set; } = false;
    public DateTime? VendorApprovedAt { get; set; }
    public string? VendorBio { get; set; }

    // Advanced Profile
    public string? BannerUrl { get; set; }
    public string? Website { get; set; }
    public string? Location { get; set; }
    public string? Jabber { get; set; }
    public DateTime? Birthday { get; set; }

    // TOTP 2FA
    public string? TotpSecret { get; set; }
    public bool Is2FAEnabled { get; set; } = false;

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
    public ICollection<MarketplaceOrder> BuyerOrders { get; set; } = [];
    public ICollection<MarketplaceOrder> SellerOrders { get; set; } = [];
    public ICollection<CryptoWallet> Wallets { get; set; } = [];
    public ICollection<WalletTransaction> WalletTransactions { get; set; } = [];
    public ICollection<VendorReview> ReviewsGiven { get; set; } = [];
    public ICollection<VendorReview> ReviewsReceived { get; set; } = [];
    public ICollection<Wishlist> Wishlists { get; set; } = [];
    public ICollection<Voucher> Vouchers { get; set; } = [];
}
