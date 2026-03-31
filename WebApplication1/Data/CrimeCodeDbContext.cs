using Microsoft.EntityFrameworkCore;
using CrimeCode.Models;

namespace CrimeCode.Data;

public class CrimeCodeDbContext : DbContext
{
    public CrimeCodeDbContext(DbContextOptions<CrimeCodeDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<PrivateMessage> PrivateMessages => Set<PrivateMessage>();
    public DbSet<Reputation> Reputations => Set<Reputation>();
    public DbSet<CreditTransaction> CreditTransactions => Set<CreditTransaction>();
    public DbSet<MarketplaceListing> MarketplaceListings => Set<MarketplaceListing>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<MarketplaceOrder> MarketplaceOrders => Set<MarketplaceOrder>();
    public DbSet<VendorApplication> VendorApplications => Set<VendorApplication>();
    public DbSet<VendorReview> VendorReviews => Set<VendorReview>();
    public DbSet<ListingImage> ListingImages => Set<ListingImage>();
    public DbSet<CryptoWallet> CryptoWallets => Set<CryptoWallet>();
    public DbSet<WalletTransaction> WalletTransactions => Set<WalletTransaction>();
    public DbSet<Voucher> Vouchers => Set<Voucher>();
    public DbSet<Wishlist> Wishlists => Set<Wishlist>();
    public DbSet<UserFollow> UserFollows => Set<UserFollow>();
    public DbSet<SupportTicket> SupportTickets => Set<SupportTicket>();
    public DbSet<TicketReply> TicketReplies => Set<TicketReply>();
    public DbSet<AdminLog> AdminLogs => Set<AdminLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.HasIndex(u => u.Username).IsUnique();
            e.HasIndex(u => u.Email).IsUnique();
        });

        modelBuilder.Entity<UserFollow>(e =>
        {
            e.HasIndex(uf => new { uf.FollowerId, uf.FollowingId }).IsUnique();
            e.HasOne(uf => uf.Follower).WithMany(u => u.Following).HasForeignKey(uf => uf.FollowerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(uf => uf.Following).WithMany(u => u.Followers).HasForeignKey(uf => uf.FollowingId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Notification>(e =>
        {
            e.HasOne(n => n.User)
             .WithMany(u => u.Notifications)
             .HasForeignKey(n => n.UserId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(n => n.FromUser)
             .WithMany()
             .HasForeignKey(n => n.FromUserId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PrivateMessage>(e =>
        {
            e.HasOne(m => m.Sender)
             .WithMany(u => u.SentMessages)
             .HasForeignKey(m => m.SenderId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(m => m.Receiver)
             .WithMany(u => u.ReceivedMessages)
             .HasForeignKey(m => m.ReceiverId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // Category hierarchy
        modelBuilder.Entity<Category>(e =>
        {
            e.HasOne(c => c.Parent)
             .WithMany(c => c.SubCategories)
             .HasForeignKey(c => c.ParentId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // Reputation
        modelBuilder.Entity<Reputation>(e =>
        {
            e.HasOne(r => r.Giver)
             .WithMany(u => u.ReputationGiven)
             .HasForeignKey(r => r.GiverId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(r => r.Receiver)
             .WithMany(u => u.ReputationReceived)
             .HasForeignKey(r => r.ReceiverId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // CreditTransaction
        modelBuilder.Entity<CreditTransaction>(e =>
        {
            e.HasOne(ct => ct.User)
             .WithMany(u => u.CreditTransactions)
             .HasForeignKey(ct => ct.UserId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(ct => ct.RelatedUser)
             .WithMany()
             .HasForeignKey(ct => ct.RelatedUserId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // Marketplace
        modelBuilder.Entity<MarketplaceListing>(e =>
        {
            e.HasOne(ml => ml.Seller)
             .WithMany(u => u.MarketplaceListings)
             .HasForeignKey(ml => ml.SellerId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(ml => ml.Category)
             .WithMany(c => c.MarketplaceListings)
             .HasForeignKey(ml => ml.CategoryId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ChatMessage
        modelBuilder.Entity<ChatMessage>(e =>
        {
            e.HasOne(cm => cm.Sender)
             .WithMany(u => u.SentChats)
             .HasForeignKey(cm => cm.SenderId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(cm => cm.Receiver)
             .WithMany(u => u.ReceivedChats)
             .HasForeignKey(cm => cm.ReceiverId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // MarketplaceOrder
        modelBuilder.Entity<MarketplaceOrder>(e =>
        {
            e.Property(o => o.Amount).HasColumnType("decimal(18,8)");

            e.HasOne(o => o.Listing)
             .WithMany(l => l.Orders)
             .HasForeignKey(o => o.ListingId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(o => o.Buyer)
             .WithMany(u => u.BuyerOrders)
             .HasForeignKey(o => o.BuyerId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(o => o.Seller)
             .WithMany(u => u.SellerOrders)
             .HasForeignKey(o => o.SellerId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // MarketplaceListing
        modelBuilder.Entity<MarketplaceListing>(e =>
        {
            e.Property(l => l.PriceCrypto).HasColumnType("decimal(18,8)");

            e.HasOne(ml => ml.Seller)
             .WithMany(u => u.MarketplaceListings)
             .HasForeignKey(ml => ml.SellerId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(ml => ml.Category)
             .WithMany(c => c.MarketplaceListings)
             .HasForeignKey(ml => ml.CategoryId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // VendorApplication
        modelBuilder.Entity<VendorApplication>(e =>
        {
            e.HasOne(va => va.User)
             .WithMany()
             .HasForeignKey(va => va.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // VendorReview
        modelBuilder.Entity<VendorReview>(e =>
        {
            e.HasIndex(vr => vr.OrderId).IsUnique(); // one review per order
            e.HasOne(vr => vr.Order).WithOne(o => o.Review).HasForeignKey<VendorReview>(vr => vr.OrderId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(vr => vr.Buyer).WithMany(u => u.ReviewsGiven).HasForeignKey(vr => vr.BuyerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(vr => vr.Seller).WithMany(u => u.ReviewsReceived).HasForeignKey(vr => vr.SellerId).OnDelete(DeleteBehavior.Restrict);
        });

        // ListingImage
        modelBuilder.Entity<ListingImage>(e =>
        {
            e.HasOne(li => li.Listing).WithMany(l => l.Images).HasForeignKey(li => li.ListingId).OnDelete(DeleteBehavior.Cascade);
        });

        // CryptoWallet
        modelBuilder.Entity<CryptoWallet>(e =>
        {
            e.Property(w => w.Balance).HasColumnType("decimal(18,8)");
            e.HasIndex(w => new { w.UserId, w.Currency }).IsUnique();
            e.HasOne(w => w.User).WithMany(u => u.Wallets).HasForeignKey(w => w.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        // WalletTransaction
        modelBuilder.Entity<WalletTransaction>(e =>
        {
            e.Property(wt => wt.Amount).HasColumnType("decimal(18,8)");
            e.HasOne(wt => wt.Wallet).WithMany().HasForeignKey(wt => wt.WalletId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(wt => wt.User).WithMany(u => u.WalletTransactions).HasForeignKey(wt => wt.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        // Voucher
        modelBuilder.Entity<Voucher>(e =>
        {
            e.HasIndex(v => v.Code).IsUnique();
            e.Property(v => v.DiscountPercent).HasColumnType("decimal(5,2)");
            e.Property(v => v.MaxDiscount).HasColumnType("decimal(18,8)");
            e.HasOne(v => v.Seller).WithMany(u => u.Vouchers).HasForeignKey(v => v.SellerId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(v => v.Listing).WithMany(l => l.Vouchers).HasForeignKey(v => v.ListingId).OnDelete(DeleteBehavior.SetNull);
        });

        // Wishlist
        modelBuilder.Entity<Wishlist>(e =>
        {
            e.HasIndex(w => new { w.UserId, w.ListingId }).IsUnique();
            e.HasOne(w => w.User).WithMany(u => u.Wishlists).HasForeignKey(w => w.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(w => w.Listing).WithMany(l => l.Wishlists).HasForeignKey(w => w.ListingId).OnDelete(DeleteBehavior.Cascade);
        });

        // MarketplaceOrder - VoucherDiscount
        modelBuilder.Entity<MarketplaceOrder>(e =>
        {
            e.Property(o => o.DiscountAmount).HasColumnType("decimal(18,8)");
        });

        // SupportTicket
        modelBuilder.Entity<SupportTicket>(e =>
        {
            e.HasOne(t => t.User).WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(t => t.AssignedTo).WithMany().HasForeignKey(t => t.AssignedToId).OnDelete(DeleteBehavior.SetNull);
        });

        // TicketReply
        modelBuilder.Entity<TicketReply>(e =>
        {
            e.HasOne(r => r.Ticket).WithMany(t => t.Replies).HasForeignKey(r => r.TicketId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(r => r.Author).WithMany().HasForeignKey(r => r.AuthorId).OnDelete(DeleteBehavior.Restrict);
        });

        // AdminLog
        modelBuilder.Entity<AdminLog>(e =>
        {
            e.HasOne(l => l.Admin).WithMany().HasForeignKey(l => l.AdminId).OnDelete(DeleteBehavior.Restrict);
        });

        // Seed categories (marketplace only)
        modelBuilder.Entity<Category>().HasData(
            new Category { Id = 6, Name = "Marketplace", Description = "Il marketplace principale - acquista e vendi", Icon = "🛒", SortOrder = 0, IsMarketplace = true },
            new Category { Id = 16, Name = "Servizi Digitali", Description = "Software, accounts e servizi digitali", Icon = "💼", SortOrder = 1, ParentId = 6, IsMarketplace = true },
            new Category { Id = 17, Name = "Risorse & Guide", Description = "E-book, corsi e materiale formativo", Icon = "📚", SortOrder = 2, ParentId = 6, IsMarketplace = true },
            new Category { Id = 18, Name = "Prodotti Fisici", Description = "Articoli fisici con spedizione", Icon = "📦", SortOrder = 3, ParentId = 6, IsMarketplace = true },
            new Category { Id = 19, Name = "Servizi Custom", Description = "Servizi personalizzati su richiesta", Icon = "⚙️", SortOrder = 4, ParentId = 6, IsMarketplace = true }
        );
    }
}
