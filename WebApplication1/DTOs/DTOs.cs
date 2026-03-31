namespace CrimeCode.DTOs;

// --- Auth ---
public record RegisterRequest(string Username, string Email, string Password);
public record LoginRequest(string Email, string Password);
public record TelegramLoginRequest(string InitData);
public record AuthResponse(int UserId, string Username, string Token);

// --- User ---
public record UserProfile(int Id, string Username, string? AvatarUrl, string? Bio, string? Signature, string Role, 
    string? CustomTitle, DateTime CreatedAt, DateTime LastSeenAt,
    int Credits, int ReputationScore,
    string Status, int FollowerCount, int FollowingCount, bool FollowedByCurrentUser,
    string? BannerUrl, string? Website, string? Location, string? Jabber, DateTime? Birthday);

// --- Category ---
public record CategoryDto(int Id, string Name, string Description, string Icon, int? ParentId, bool IsMarketplace, List<CategoryDto>? SubCategories);

// --- Follow ---
public record FollowDto(int Id, string Username, string? AvatarUrl, DateTime FollowedAt);

// --- User Status ---
public record UpdateStatusRequest(string Status);

// --- Admin ---
public record AdminUserDto(int Id, string Username, string Email, string Role, DateTime CreatedAt, 
    int Credits, int ReputationScore, bool IsBanned);
public record UpdateUserRoleRequest(string Role);
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
public record CreateListingRequest(string Title, string Description, decimal PriceCrypto, string Currency, string Type, string DeliveryType, int CategoryId, string? DigitalContent, string? ShippingInfo, string? ImageUrl, int Stock);
public record ListingDto(int Id, string Title, string Description, decimal PriceCrypto, string Currency, string Type, string Status, string DeliveryType,
    DateTime CreatedAt, string SellerName, int SellerId, string? SellerAvatarUrl, bool IsVendor, string CategoryName, int CategoryId,
    string? ImageUrl, int Stock, int SoldCount, string? RejectionReason, string? ShippingInfo);
public record ListingDetailDto(int Id, string Title, string Description, decimal PriceCrypto, string Currency, string Type, string Status, string DeliveryType,
    DateTime CreatedAt, string SellerName, int SellerId, string? SellerAvatarUrl, bool IsVendor, string CategoryName, int CategoryId,
    string? ImageUrl, int Stock, int SoldCount, string? ShippingInfo, int SellerReputation, int SellerSalesCount, string? VendorBio);

// --- Vendor ---
public record VendorApplicationRequest(string TelegramUsername, string Motivation, string? Specialization);
public record VendorApplicationDto(int Id, int UserId, string Username, string? AvatarUrl, string TelegramUsername, string Motivation, string? Specialization, string Status, string? ReviewNote, DateTime CreatedAt, DateTime? ReviewedAt);
public record VendorReviewRequest(string Status, string? ReviewNote); // Approved or Rejected
public record VendorProfileDto(int Id, string Username, string? AvatarUrl, string? VendorBio, DateTime? VendorSince, int ReputationScore, int TotalSales, int ActiveListings, decimal TotalRevenue);

// --- Orders / Escrow ---
public record CreateOrderRequest(int ListingId, int Quantity, string? ShippingAddress);
public record OrderDto(int Id, int ListingId, string ListingTitle, string? ListingImageUrl, int BuyerId, string BuyerName, int SellerId, string SellerName, 
    decimal Amount, string Currency, int Quantity, string Status, string EscrowStatus, string DeliveryType,
    string? ShippingAddress, string? TrackingNumber, bool IsDelivered, string? EscrowWalletAddress,
    string? DigitalDeliveryContent, string? DisputeReason, DateTime CreatedAt, DateTime? UpdatedAt);
public record FundEscrowRequest(string BuyerTxId);
public record ShipOrderRequest(string TrackingNumber);
public record DisputeOrderRequest(string Reason);

// --- Live Chat ---
public record ChatMessageDto(int Id, string Content, DateTime CreatedAt, bool IsRead, int SenderId, string SenderName, string? SenderAvatarUrl, int ReceiverId, string ReceiverName);
public record SendChatRequest(string Content);
public record ChatContactDto(int UserId, string Username, string? AvatarUrl, string Status, string? LastMessage, DateTime? LastMessageAt, int UnreadCount);

// --- Vendor Leaderboard ---
public record VendorLeaderboardEntry(int Id, string Username, string? AvatarUrl, string? VendorBio,
    int TotalSales, double AvgRating, int ReviewCount, int ReputationScore, decimal TotalRevenue);

// --- Online Users ---
public record OnlineUserDto(int Id, string Username, string? AvatarUrl, string Role, string Status);

// --- Marketplace Stats ---
public record MarketplaceStatsDto(int TotalUsers, int TotalListings, int TotalSales, int OnlineUsers, string? NewestMember);

// --- Vendor Reviews ---
public record CreateReviewRequest(int OrderId, int Rating, string? Comment);
public record VendorReviewDto(int Id, int OrderId, int Rating, string? Comment, DateTime CreatedAt, 
    int BuyerId, string BuyerName, string? BuyerAvatarUrl, int SellerId, string SellerName);

// --- Listing Images ---
public record ListingImageDto(int Id, string Url, int SortOrder);
public record AddListingImagesRequest(List<string> ImageUrls);

// --- Crypto Wallet ---
public record WalletDto(int Id, string Currency, decimal Balance, DateTime UpdatedAt);
public record WalletTransactionDto(int Id, decimal Amount, string Type, string? Reference, string Currency, DateTime CreatedAt);
public record DepositRequest(string Currency, decimal Amount, string TxId);
public record WithdrawRequest(string Currency, decimal Amount, string WalletAddress);

// --- Voucher ---
public record CreateVoucherRequest(string Code, decimal DiscountPercent, decimal? MaxDiscount, int MaxUses, DateTime? ExpiresAt, int? ListingId);
public record VoucherDto(int Id, string Code, decimal DiscountPercent, decimal? MaxDiscount, int MaxUses, int UsedCount, bool IsActive, DateTime? ExpiresAt, 
    int SellerId, string SellerName, int? ListingId, string? ListingTitle);
public record ApplyVoucherRequest(string Code, int ListingId);
public record VoucherCheckResult(bool Valid, decimal DiscountPercent, decimal? MaxDiscount, string? Error);

// --- Wishlist ---
public record WishlistDto(int Id, int ListingId, string ListingTitle, string? ListingImageUrl, decimal PriceCrypto, string Currency, string SellerName, DateTime AddedAt);
public record ToggleWishlistRequest(int ListingId);

// --- 2FA TOTP ---
public record TotpSetupResponse(string Secret, string QrCodeUri);
public record TotpVerifyRequest(string Code);
public record LoginWith2FARequest(string Email, string Password, string? TotpCode);

// --- Vendor Stats ---
public record VendorStatsDto(int TotalSales, decimal TotalRevenue, int ActiveListings, int PendingOrders, int DisputedOrders, 
    double AverageRating, int TotalReviews, decimal MonthlyRevenue, int MonthlySales, List<MonthlyStat> Last6Months);
public record MonthlyStat(string Month, int Sales, decimal Revenue);

// --- Enhanced Listing DTOs ---
public record ListingDetailExtendedDto(int Id, string Title, string Description, decimal PriceCrypto, string Currency, string Type, string Status, string DeliveryType,
    DateTime CreatedAt, string SellerName, int SellerId, string? SellerAvatarUrl, bool IsVendor, string CategoryName, int CategoryId,
    string? ImageUrl, int Stock, int SoldCount, string? ShippingInfo, int SellerReputation, int SellerSalesCount, string? VendorBio,
    List<ListingImageDto> Images, List<VendorReviewDto> SellerReviews, double SellerAvgRating, bool InWishlist);

// --- Marketplace Search ---
public record MarketplaceSearchRequest(string? Query, int? CategoryId, string? Currency, decimal? MinPrice, decimal? MaxPrice, string? Type, string? SortBy, int Page = 1, int PageSize = 20);

// --- Admin Marketplace ---
public record AdminListingDto(int Id, string Title, string SellerName, int SellerId, string Status, string CategoryName, decimal PriceCrypto, string Currency, DateTime CreatedAt, string? RejectionReason);
public record AdminReviewListingRequest(string Status, string? RejectionReason); // Approved or Rejected
public record AdminDisputeDto(int OrderId, string ListingTitle, string BuyerName, string SellerName, string? DisputeReason, decimal Amount, string Currency, string Status, DateTime CreatedAt);
public record AdminResolveDisputeRequest(string Resolution, string? Note); // RefundBuyer or ReleaseSeller
