namespace CrimeCode.DTOs;

// --- Auth ---
public record RegisterRequest(string Username, string Email, string Password);
public record LoginRequest(string Email, string Password);
public record TelegramLoginRequest(string InitData);
public record AuthResponse(int UserId, string Username, string Token);

// --- User ---
public record UserProfile(int Id, string Username, string? AvatarUrl, string? Bio, string? Signature, string Role, 
    string? CustomTitle, DateTime CreatedAt, DateTime LastSeenAt, int ThreadCount, int PostCount, 
    int Credits, int ReputationScore, string RankName, string RankColor, string RankIcon,
    string Status, int FollowerCount, int FollowingCount, bool FollowedByCurrentUser);

// --- Category ---
public record CategoryDto(int Id, string Name, string Description, string Icon, int ThreadCount, int? ParentId, bool IsMarketplace, List<CategoryDto>? SubCategories);

// --- Thread ---
public record CreateThreadRequest(string Title, string Content, int CategoryId, int? TagId, string? Prefix);
public record ThreadSummaryDto(int Id, string Title, string? Prefix, string AuthorName, int AuthorId, string? AuthorAvatarUrl, 
    string CategoryName, int PostCount, int ViewCount, bool IsPinned, bool IsLocked, 
    DateTime CreatedAt, DateTime? LastActivityAt, string? TagName, string? TagColor, string? LastPostAuthor, DateTime? LastPostAt);
public record ThreadDetailDto(int Id, string Title, string? Prefix, string AuthorName, int AuthorId, string? AuthorAvatarUrl,
    string CategoryName, int CategoryId, int ViewCount, bool IsPinned, bool IsLocked, 
    DateTime CreatedAt, List<PostDto> Posts, string? TagName, string? TagColor);

// --- Post ---
public record CreatePostRequest(string Content, int? ParentPostId);
public record PostDto(int Id, string Content, string AuthorName, int AuthorId, string? AuthorAvatarUrl, 
    string AuthorRole, string RankName, string RankColor, string RankIcon, string? AuthorSignature,
    int AuthorPostCount, int AuthorReputation, DateTime AuthorJoinDate,
    DateTime CreatedAt, DateTime? EditedAt, int LikeCount, bool LikedByCurrentUser, 
    int? ParentPostId, List<PostDto> Replies,
    Dictionary<string, int> Reactions, List<string> CurrentUserReactions, List<AttachmentDto> Attachments);

// --- Attachment ---
public record AttachmentDto(int Id, string FileName, string Url, string ContentType, long FileSizeBytes);

// --- Reaction ---
public record AddReactionRequest(string Emoji);

// --- Follow ---
public record FollowDto(int Id, string Username, string? AvatarUrl, string RankName, string RankColor, DateTime FollowedAt);

// --- Search ---
public record AdvancedSearchRequest(string? Query, int? CategoryId, int? AuthorId, int? TagId, string? DateFrom, string? DateTo, int Page = 1, int PageSize = 20);

// --- User Status ---
public record UpdateStatusRequest(string Status);

// --- Admin ---
public record AdminUserDto(int Id, string Username, string Email, string Role, DateTime CreatedAt, 
    int ThreadCount, int PostCount, int Credits, int ReputationScore, bool IsBanned);
public record UpdateUserRoleRequest(string Role);
public record AdminThreadDto(int Id, string Title, string AuthorName, string CategoryName, int PostCount, bool IsPinned, bool IsLocked, DateTime CreatedAt);
public record UpdateThreadRequest(bool? IsPinned, bool? IsLocked);
public record AdminBanRequest(string? Reason);
public record AdminCreditsRequest(int Amount, string Reason);

// --- Notification ---
public record NotificationDto(int Id, string Type, string Message, bool IsRead, DateTime CreatedAt, string? FromUsername, int? ThreadId, int? PostId);

// --- Private Message ---
public record SendMessageRequest(int ReceiverId, string Content);
public record MessageDto(int Id, string Content, bool IsRead, DateTime CreatedAt, int SenderId, string SenderName, string? SenderAvatarUrl, int ReceiverId, string ReceiverName);
public record ConversationDto(int UserId, string Username, string? AvatarUrl, string LastMessage, DateTime LastMessageAt, int UnreadCount);

// --- Reputation ---
public record GiveReputationRequest(int UserId, int Points, string? Comment);
public record ReputationDto(int Id, int Points, string? Comment, DateTime CreatedAt, string GiverName, int GiverId, string ReceiverName, int ReceiverId);

// --- Marketplace ---
public record CreateListingRequest(string Title, string Description, int Price, string Type, int CategoryId);
public record ListingDto(int Id, string Title, string Description, int Price, string Type, string Status, 
    DateTime CreatedAt, string SellerName, int SellerId, string? SellerAvatarUrl, string CategoryName);

// --- Shoutbox ---
public record ShoutboxDto(int Id, string Content, DateTime CreatedAt, string AuthorName, int AuthorId, string? AuthorAvatarUrl, string AuthorRole);
public record ShoutboxPostRequest(string Content);

// --- Leaderboard ---
public record LeaderboardEntry(int Id, string Username, string? AvatarUrl, string Role, string RankName, string RankColor, string RankIcon,
    int PostCount, int ThreadCount, int ReputationScore, int Credits);

// --- Online Users ---
public record OnlineUserDto(int Id, string Username, string? AvatarUrl, string Role, string RankName, string RankColor, string Status);

// --- Thread Tags ---
public record ThreadTagDto(int Id, string Name, string Color);

// --- User Ranks ---
public record UserRankDto(int Id, string Name, string Color, string Icon, int MinPosts, int MinReputation);

// --- Forum Stats ---
public record ForumStatsDto(int TotalUsers, int TotalThreads, int TotalPosts, int OnlineUsers, string? NewestMember);
