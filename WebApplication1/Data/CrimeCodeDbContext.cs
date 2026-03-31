using Microsoft.EntityFrameworkCore;
using CrimeCode.Models;

namespace CrimeCode.Data;

public class CrimeCodeDbContext : DbContext
{
    public CrimeCodeDbContext(DbContextOptions<CrimeCodeDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<ForumThread> Threads => Set<ForumThread>();
    public DbSet<Post> Posts => Set<Post>();
    public DbSet<PostLike> PostLikes => Set<PostLike>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<PrivateMessage> PrivateMessages => Set<PrivateMessage>();
    public DbSet<Reputation> Reputations => Set<Reputation>();
    public DbSet<CreditTransaction> CreditTransactions => Set<CreditTransaction>();
    public DbSet<MarketplaceListing> MarketplaceListings => Set<MarketplaceListing>();
    public DbSet<UserRank> UserRanks => Set<UserRank>();
    public DbSet<ThreadTag> ThreadTags => Set<ThreadTag>();
    public DbSet<ShoutboxMessage> ShoutboxMessages => Set<ShoutboxMessage>();
    public DbSet<PostReaction> PostReactions => Set<PostReaction>();
    public DbSet<UserFollow> UserFollows => Set<UserFollow>();
    public DbSet<PostAttachment> PostAttachments => Set<PostAttachment>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<MarketplaceOrder> MarketplaceOrders => Set<MarketplaceOrder>();
    public DbSet<VendorApplication> VendorApplications => Set<VendorApplication>();
    public DbSet<VendorReview> VendorReviews => Set<VendorReview>();
    public DbSet<ListingImage> ListingImages => Set<ListingImage>();
    public DbSet<CryptoWallet> CryptoWallets => Set<CryptoWallet>();
    public DbSet<WalletTransaction> WalletTransactions => Set<WalletTransaction>();
    public DbSet<Voucher> Vouchers => Set<Voucher>();
    public DbSet<Wishlist> Wishlists => Set<Wishlist>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.HasIndex(u => u.Username).IsUnique();
            e.HasIndex(u => u.Email).IsUnique();
        });

        modelBuilder.Entity<PostLike>(e =>
        {
            e.HasIndex(pl => new { pl.UserId, pl.PostId }).IsUnique();
        });

        modelBuilder.Entity<PostReaction>(e =>
        {
            e.HasIndex(pr => new { pr.UserId, pr.PostId, pr.Emoji }).IsUnique();
            e.HasOne(pr => pr.Post).WithMany(p => p.Reactions).HasForeignKey(pr => pr.PostId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(pr => pr.User).WithMany(u => u.Reactions).HasForeignKey(pr => pr.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserFollow>(e =>
        {
            e.HasIndex(uf => new { uf.FollowerId, uf.FollowingId }).IsUnique();
            e.HasOne(uf => uf.Follower).WithMany(u => u.Following).HasForeignKey(uf => uf.FollowerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(uf => uf.Following).WithMany(u => u.Followers).HasForeignKey(uf => uf.FollowingId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PostAttachment>(e =>
        {
            e.HasOne(pa => pa.Post).WithMany(p => p.Attachments).HasForeignKey(pa => pa.PostId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(pa => pa.Uploader).WithMany(u => u.Attachments).HasForeignKey(pa => pa.UploaderId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Post>(e =>
        {
            e.HasOne(p => p.ParentPost)
             .WithMany(p => p.Replies)
             .HasForeignKey(p => p.ParentPostId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(p => p.Thread)
             .WithMany(t => t.Posts)
             .HasForeignKey(p => p.ThreadId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(p => p.Author)
             .WithMany(u => u.Posts)
             .HasForeignKey(p => p.AuthorId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ForumThread>(e =>
        {
            e.HasOne(t => t.Author)
             .WithMany(u => u.Threads)
             .HasForeignKey(t => t.AuthorId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(t => t.Tag)
             .WithMany()
             .HasForeignKey(t => t.TagId)
             .OnDelete(DeleteBehavior.SetNull);
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

        // ShoutboxMessage
        modelBuilder.Entity<ShoutboxMessage>(e =>
        {
            e.HasOne(s => s.Author)
             .WithMany(u => u.ShoutboxMessages)
             .HasForeignKey(s => s.AuthorId)
             .OnDelete(DeleteBehavior.Cascade);
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

        // Seed categories (with sections/subsections)
        modelBuilder.Entity<Category>().HasData(
            // Main sections
            new Category { Id = 1, Name = "Generale", Description = "Discussioni generali, presentazioni e annunci", Icon = "💬", SortOrder = 1 },
            new Category { Id = 2, Name = "Cybersecurity", Description = "Sicurezza informatica, CTF e ethical hacking", Icon = "🔒", SortOrder = 2 },
            new Category { Id = 3, Name = "Programmazione", Description = "Linguaggi, framework e development", Icon = "💻", SortOrder = 3 },
            new Category { Id = 4, Name = "Networking", Description = "Reti, protocolli e infrastrutture", Icon = "🌐", SortOrder = 4 },
            new Category { Id = 5, Name = "Reverse Engineering", Description = "Analisi binaria e malware analysis", Icon = "🔍", SortOrder = 5 },
            new Category { Id = 6, Name = "Marketplace", Description = "Il marketplace principale - acquista e vendi", Icon = "🛒", SortOrder = 0, IsMarketplace = true },
            new Category { Id = 7, Name = "Off-Topic", Description = "Tutto ciò che non rientra nelle altre categorie", Icon = "🎲", SortOrder = 7 },
            // Sub-categories
            new Category { Id = 8, Name = "Presentazioni", Description = "Presentati alla community", Icon = "👋", SortOrder = 1, ParentId = 1 },
            new Category { Id = 9, Name = "Annunci", Description = "Annunci ufficiali dello staff", Icon = "📢", SortOrder = 2, ParentId = 1 },
            new Category { Id = 10, Name = "Pentesting", Description = "Tool e tecniche di penetration testing", Icon = "🎯", SortOrder = 1, ParentId = 2 },
            new Category { Id = 11, Name = "OSINT", Description = "Open Source Intelligence", Icon = "🕵️", SortOrder = 2, ParentId = 2 },
            new Category { Id = 12, Name = "Crittografia", Description = "Algoritmi e protocolli crittografici", Icon = "🔐", SortOrder = 3, ParentId = 2 },
            new Category { Id = 13, Name = "Web Development", Description = "Frontend, Backend, Full Stack", Icon = "🌍", SortOrder = 1, ParentId = 3 },
            new Category { Id = 14, Name = "Python", Description = "Scripts, automazione e tool in Python", Icon = "🐍", SortOrder = 2, ParentId = 3 },
            new Category { Id = 15, Name = "Tools & Scripts", Description = "Tool personalizzati e script utili", Icon = "🔧", SortOrder = 3, ParentId = 3 },
            // Marketplace sub-categories
            new Category { Id = 16, Name = "Servizi Digitali", Description = "Software, accounts e servizi digitali", Icon = "💼", SortOrder = 1, ParentId = 6, IsMarketplace = true },
            new Category { Id = 17, Name = "Risorse & Guide", Description = "E-book, corsi e materiale formativo", Icon = "📚", SortOrder = 2, ParentId = 6, IsMarketplace = true },
            new Category { Id = 18, Name = "Prodotti Fisici", Description = "Articoli fisici con spedizione", Icon = "📦", SortOrder = 3, ParentId = 6, IsMarketplace = true },
            new Category { Id = 19, Name = "Servizi Custom", Description = "Servizi personalizzati su richiesta", Icon = "⚙️", SortOrder = 4, ParentId = 6, IsMarketplace = true }
        );

        // Seed thread tags
        modelBuilder.Entity<ThreadTag>().HasData(
            new ThreadTag { Id = 1, Name = "Tutorial", Color = "#00e5a0" },
            new ThreadTag { Id = 2, Name = "Domanda", Color = "#00d4ff" },
            new ThreadTag { Id = 3, Name = "Release", Color = "#ff9900" },
            new ThreadTag { Id = 4, Name = "Guida", Color = "#a855f7" },
            new ThreadTag { Id = 5, Name = "Tool", Color = "#ff2244" },
            new ThreadTag { Id = 6, Name = "Discussione", Color = "#6b7fa0" },
            new ThreadTag { Id = 7, Name = "Risolto", Color = "#22c55e" },
            new ThreadTag { Id = 8, Name = "WIP", Color = "#eab308" }
        );

        // Seed user ranks
        modelBuilder.Entity<UserRank>().HasData(
            new UserRank { Id = 1, Name = "Newbie", Color = "#6b7fa0", Icon = "🔰", MinPosts = 0, MinReputation = 0, SortOrder = 1 },
            new UserRank { Id = 2, Name = "Member", Color = "#d5dbe8", Icon = "👤", MinPosts = 10, MinReputation = 0, SortOrder = 2 },
            new UserRank { Id = 3, Name = "Active", Color = "#00d4ff", Icon = "⚡", MinPosts = 50, MinReputation = 5, SortOrder = 3 },
            new UserRank { Id = 4, Name = "Veteran", Color = "#a855f7", Icon = "🏆", MinPosts = 200, MinReputation = 20, SortOrder = 4 },
            new UserRank { Id = 5, Name = "Elite", Color = "#ff9900", Icon = "👑", MinPosts = 500, MinReputation = 50, SortOrder = 5 },
            new UserRank { Id = 6, Name = "Legend", Color = "#ff2244", Icon = "🔥", MinPosts = 1000, MinReputation = 100, SortOrder = 6 }
        );
    }
}
