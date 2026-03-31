using System.Text;
using CrimeCode.Data;
using CrimeCode.Endpoints;
using CrimeCode.Models;
using CrimeCode.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Database
builder.Services.AddDbContext<CrimeCodeDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=crimecode.db"));

// Auth
var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key not configured in appsettings.json");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "CrimeCode";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtIssuer,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddSingleton<AuthService>();

var app = builder.Build();

// Ensure DB is created and seeded
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CrimeCodeDbContext>();
    db.Database.EnsureCreated();

    // Seed demo marketplace data if no users exist
    if (!db.Users.Any())
    {
        var now = DateTime.UtcNow;
        var hash = BCrypt.Net.BCrypt.HashPassword("admin123");
        
        // Create admin + demo vendors
        var admin = new User { Username = "Admin", Email = "admin@crimecode.it", PasswordHash = hash, Role = "Admin", CreatedAt = now.AddDays(-90), ReputationScore = 50, Credits = 999 };
        var v1 = new User { Username = "CyberVendor", Email = "cyber@vendor.cc", PasswordHash = hash, Role = "User", IsVendor = true, VendorApprovedAt = now.AddDays(-60), VendorBio = "Specialista in tool di sicurezza e guide premium", CreatedAt = now.AddDays(-60), ReputationScore = 35, Credits = 500 };
        var v2 = new User { Username = "DataShark", Email = "shark@vendor.cc", PasswordHash = hash, Role = "User", IsVendor = true, VendorApprovedAt = now.AddDays(-45), VendorBio = "Database, risorse OSINT e servizi custom", CreatedAt = now.AddDays(-45), ReputationScore = 28, Credits = 300 };
        var v3 = new User { Username = "GhostDev", Email = "ghost@vendor.cc", PasswordHash = hash, Role = "User", IsVendor = true, VendorApprovedAt = now.AddDays(-30), VendorBio = "Sviluppatore full-stack, bot custom e automazioni", CreatedAt = now.AddDays(-30), ReputationScore = 20, Credits = 150 };
        var u1 = new User { Username = "H4cker_N00b", Email = "noob@user.cc", PasswordHash = hash, Role = "User", CreatedAt = now.AddDays(-20), ReputationScore = 5, Credits = 50 };
        var u2 = new User { Username = "ShadowByte", Email = "shadow@user.cc", PasswordHash = hash, Role = "User", CreatedAt = now.AddDays(-15), ReputationScore = 3, Credits = 30 };
        db.Users.AddRange(admin, v1, v2, v3, u1, u2);
        db.SaveChanges();

        // Create marketplace listings
        var listings = new List<MarketplaceListing>
        {
            new() { Title = "Guida Completa Pentesting 2025", Description = "Guida aggiornata con 200+ pagine su tecniche di penetration testing. Include: Recon, Enumeration, Exploitation, Post-Exploitation e Report Writing. PDF + Video tutorial.", PriceCrypto = 0.002m, Currency = "BTC", Type = "Digital", DeliveryType = "Instant", CategoryId = 17, Stock = 999, SoldCount = 47, SellerId = v1.Id, Status = "Active", CreatedAt = now.AddDays(-55), ImageUrl = "https://placehold.co/400x300/1a1f2e/00e5a0?text=Pentesting+Guide", DigitalContent = "Link download: [redatto per demo]" },
            new() { Title = "Tool OSINT Automatizzato v3.2", Description = "Script Python all-in-one per OSINT: ricerca email, social media, domini, IP geolocation, data breaches. Aggiornamenti gratuiti per 6 mesi.", PriceCrypto = 0.005m, Currency = "BTC", Type = "Digital", DeliveryType = "Instant", CategoryId = 16, Stock = 50, SoldCount = 23, SellerId = v2.Id, Status = "Active", CreatedAt = now.AddDays(-40), ImageUrl = "https://placehold.co/400x300/1a1f2e/00d4ff?text=OSINT+Tool" },
            new() { Title = "Bot Telegram Custom — Sviluppo Su Misura", Description = "Sviluppo bot Telegram personalizzati: scraping, automazioni, notifiche, gestione gruppi, comandi custom. Consegna in 48-72h. Supporto post-vendita incluso.", PriceCrypto = 50m, Currency = "USDT", Type = "Service", DeliveryType = "Manual", CategoryId = 19, Stock = 10, SoldCount = 12, SellerId = v3.Id, Status = "Active", CreatedAt = now.AddDays(-28), ImageUrl = "https://placehold.co/400x300/1a1f2e/a855f7?text=Telegram+Bot" },
            new() { Title = "Corso Ethical Hacking — Da Zero a Pro", Description = "Corso completo in italiano: 40+ ore di video, laboratori pratici, CTF private, certificato di completamento. Accesso a vita + community Discord esclusiva.", PriceCrypto = 0.008m, Currency = "BTC", Type = "Digital", DeliveryType = "Instant", CategoryId = 17, Stock = 200, SoldCount = 89, SellerId = v1.Id, Status = "Active", CreatedAt = now.AddDays(-50), ImageUrl = "https://placehold.co/400x300/1a1f2e/ff9900?text=Ethical+Hacking" },
            new() { Title = "Database OSINT Collection 2025", Description = "Collezione curata di 500+ risorse OSINT: motori di ricerca, API gratuite, framework, tool e tecniche. Aggiornato mensilmente.", PriceCrypto = 25m, Currency = "USDT", Type = "Digital", DeliveryType = "Instant", CategoryId = 17, Stock = 999, SoldCount = 156, SellerId = v2.Id, Status = "Active", CreatedAt = now.AddDays(-35), ImageUrl = "https://placehold.co/400x300/1a1f2e/22c55e?text=OSINT+DB" },
            new() { Title = "VPN Config Premium — 50 Server", Description = "Configurazioni VPN premium per OpenVPN/WireGuard: 50 server in 20 paesi, velocità elevata, no-log garantito. Durata 3 mesi.", PriceCrypto = 15m, Currency = "USDT", Type = "Digital", DeliveryType = "Instant", CategoryId = 16, Stock = 30, SoldCount = 18, SellerId = v1.Id, Status = "Active", CreatedAt = now.AddDays(-20), ImageUrl = "https://placehold.co/400x300/1a1f2e/ff2244?text=VPN+Premium" },
            new() { Title = "Script Automazione Social Media", Description = "Suite di script Python per automazione social: scheduling post, analytics, follower analysis, sentiment analysis. Compatibile con le principali piattaforme.", PriceCrypto = 35m, Currency = "USDT", Type = "Digital", DeliveryType = "Instant", CategoryId = 16, Stock = 75, SoldCount = 31, SellerId = v3.Id, Status = "Active", CreatedAt = now.AddDays(-15), ImageUrl = "https://placehold.co/400x300/1a1f2e/eab308?text=Social+Scripts" },
            new() { Title = "Web Scraping Service — Dati Su Misura", Description = "Servizio professionale di web scraping: raccolta dati da qualsiasi sito, pulizia e formattazione in CSV/JSON/Excel. Preventivo gratuito.", PriceCrypto = 0.003m, Currency = "BTC", Type = "Service", DeliveryType = "Manual", CategoryId = 19, Stock = 20, SoldCount = 8, SellerId = v2.Id, Status = "Active", CreatedAt = now.AddDays(-10), ImageUrl = "https://placehold.co/400x300/1a1f2e/6b7fa0?text=Scraping" },
            new() { Title = "Flipper Zero — Firmware Custom", Description = "Flipper Zero con firmware custom pre-installato: BadUSB scripts, NFC tools, Sub-GHz extended range. Spedizione anonima in 5-7gg.", PriceCrypto = 0.012m, Currency = "BTC", Type = "Physical", DeliveryType = "Shipping", CategoryId = 18, Stock = 5, SoldCount = 3, SellerId = v1.Id, Status = "Active", CreatedAt = now.AddDays(-8), ImageUrl = "https://placehold.co/400x300/1a1f2e/ff9900?text=Flipper+Zero", ShippingInfo = "Spedizione anonima via corriere, 5-7 giorni lavorativi. Tracking fornito." },
            new() { Title = "Wifi Pineapple — Pentesting Kit", Description = "Kit completo per wireless pentesting: Wifi Pineapple + antenne direzionali + guida setup. Ideale per audit di rete autorizzati.", PriceCrypto = 0.015m, Currency = "BTC", Type = "Physical", DeliveryType = "Shipping", CategoryId = 18, Stock = 3, SoldCount = 2, SellerId = v1.Id, Status = "Active", CreatedAt = now.AddDays(-5), ImageUrl = "https://placehold.co/400x300/1a1f2e/00d4ff?text=Wifi+Pineapple", ShippingInfo = "Spedizione discreta, 7-10 giorni lavorativi." },
            new() { Title = "Analisi Vulnerabilità Sito Web", Description = "Audit completo di sicurezza per il tuo sito web: vulnerability assessment, report dettagliato con remediation plan. Tempo: 3-5 giorni.", PriceCrypto = 100m, Currency = "USDT", Type = "Service", DeliveryType = "Manual", CategoryId = 19, Stock = 5, SoldCount = 6, SellerId = v3.Id, Status = "Active", CreatedAt = now.AddDays(-3), ImageUrl = "https://placehold.co/400x300/1a1f2e/ef4444?text=Vuln+Scan" },
            new() { Title = "Pack Wallpaper Hacker Aesthetic 4K", Description = "100+ wallpaper in 4K tema hacker/cyberpunk: matrix, terminali, code rain, circuit board. Per desktop e mobile.", PriceCrypto = 5m, Currency = "USDT", Type = "Digital", DeliveryType = "Instant", CategoryId = 17, Stock = 999, SoldCount = 234, SellerId = v2.Id, Status = "Active", CreatedAt = now.AddDays(-42), ImageUrl = "https://placehold.co/400x300/1a1f2e/a855f7?text=Wallpapers+4K" },
        };
        db.MarketplaceListings.AddRange(listings);
        db.SaveChanges();

        // Create some demo orders (completed)
        var orders = new List<MarketplaceOrder>
        {
            new() { ListingId = listings[0].Id, BuyerId = u1.Id, SellerId = v1.Id, Quantity = 1, Amount = listings[0].PriceCrypto, Currency = "BTC", Status = "Completed", EscrowStatus = "Released", DeliveryType = "Instant", CreatedAt = now.AddDays(-30) },
            new() { ListingId = listings[1].Id, BuyerId = u2.Id, SellerId = v2.Id, Quantity = 1, Amount = listings[1].PriceCrypto, Currency = "BTC", Status = "Completed", EscrowStatus = "Released", DeliveryType = "Instant", CreatedAt = now.AddDays(-20) },
            new() { ListingId = listings[4].Id, BuyerId = u1.Id, SellerId = v2.Id, Quantity = 1, Amount = listings[4].PriceCrypto, Currency = "USDT", Status = "Completed", EscrowStatus = "Released", DeliveryType = "Instant", CreatedAt = now.AddDays(-15) },
        };
        db.MarketplaceOrders.AddRange(orders);
        db.SaveChanges();

        // Create some reviews
        db.VendorReviews.AddRange(
            new VendorReview { OrderId = orders[0].Id, BuyerId = u1.Id, SellerId = v1.Id, Rating = 5, Comment = "Guida eccellente, molto dettagliata. Consigliato!", CreatedAt = now.AddDays(-29) },
            new VendorReview { OrderId = orders[1].Id, BuyerId = u2.Id, SellerId = v2.Id, Rating = 4, Comment = "Tool funzionante, buon supporto.", CreatedAt = now.AddDays(-19) },
            new VendorReview { OrderId = orders[2].Id, BuyerId = u1.Id, SellerId = v2.Id, Rating = 5, Comment = "Raccolta vastissima, vale ogni satoshi!", CreatedAt = now.AddDays(-14) }
        );

        // Create forum threads with initial posts
        var threads = new List<ForumThread>
        {
            new() { Title = "Benvenuti su CrimeCode!", AuthorId = admin.Id, CategoryId = 9, IsPinned = true, CreatedAt = now.AddDays(-90), LastActivityAt = now.AddDays(-1), ViewCount = 350 },
            new() { Title = "Guida: Come iniziare con il Pentesting", AuthorId = v1.Id, CategoryId = 10, CreatedAt = now.AddDays(-45), LastActivityAt = now.AddDays(-5), ViewCount = 180, TagId = 1 },
            new() { Title = "Release: OSINT Framework v2.0", AuthorId = v2.Id, CategoryId = 11, CreatedAt = now.AddDays(-30), LastActivityAt = now.AddDays(-10), ViewCount = 95, TagId = 3 },
            new() { Title = "Python: Script per SYN Scan personalizzato", AuthorId = v3.Id, CategoryId = 14, CreatedAt = now.AddDays(-20), LastActivityAt = now.AddDays(-12), ViewCount = 60, TagId = 5 },
            new() { Title = "Mi presento — Nuovo membro!", AuthorId = u1.Id, CategoryId = 8, CreatedAt = now.AddDays(-18), LastActivityAt = now.AddDays(-15), ViewCount = 25 },
            new() { Title = "Discussione: Quale certificazione cybersec?", AuthorId = u2.Id, CategoryId = 10, CreatedAt = now.AddDays(-10), LastActivityAt = now.AddDays(-3), ViewCount = 40, TagId = 2 },
        };
        db.Threads.AddRange(threads);
        db.SaveChanges();

        // Add first post for each thread
        db.Posts.AddRange(
            new Post { ThreadId = threads[0].Id, AuthorId = admin.Id, Content = "Benvenuti nella community underground italiana dedicata a cybersecurity, hacking etico e tecnologia. Leggete le regole e presentatevi!", CreatedAt = now.AddDays(-90) },
            new Post { ThreadId = threads[1].Id, AuthorId = v1.Id, Content = "In questa guida vi spiegherò i fondamentali del penetration testing: dalla recon all'exploitation. Tool consigliati: Nmap, Burp Suite, Metasploit, Hashcat, John the Ripper.", CreatedAt = now.AddDays(-45) },
            new Post { ThreadId = threads[2].Id, AuthorId = v2.Id, Content = "Ho rilasciato la versione 2.0 del mio framework OSINT open source. Nuove features: geolocation avanzata, social graph analysis, API integration per le principali piattaforme.", CreatedAt = now.AddDays(-30) },
            new Post { ThreadId = threads[3].Id, AuthorId = v3.Id, Content = "Condivido il mio script Python per un SYN scan personalizzato usando Scapy. Più veloce e configurabile di Nmap per scan specifici. Source code nel post seguente.", CreatedAt = now.AddDays(-20) },
            new Post { ThreadId = threads[4].Id, AuthorId = u1.Id, Content = "Ciao a tutti! Sono nuovo qui, mi interesso di cybersecurity da qualche anno. Lavoro come sysadmin e studio pentesting nel tempo libero. Contento di far parte della community!", CreatedAt = now.AddDays(-18) },
            new Post { ThreadId = threads[5].Id, AuthorId = u2.Id, Content = "Sto valutando quale certificazione prendere: CEH, OSCP, CompTIA Security+? Quali consigliate per chi è alle prime armi ma con buone basi tecniche?", CreatedAt = now.AddDays(-10) }
        );
        db.SaveChanges();
    }
}

// Serve static files (wwwroot)
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

// Map API endpoints
app.MapAuthEndpoints();
app.MapTelegramAuthEndpoints();
app.MapCategoryEndpoints();
app.MapThreadEndpoints();
app.MapPostEndpoints();
app.MapUserEndpoints();
app.MapAdminEndpoints();
app.MapAvatarEndpoints();
app.MapNotificationEndpoints();
app.MapMessageEndpoints();
app.MapReputationEndpoints();
app.MapMarketplaceEndpoints();
app.MapVendorEndpoints();
app.MapOrderEndpoints();
app.MapShoutboxEndpoints();
app.MapLeaderboardEndpoints();
app.MapReactionsEndpoints();
app.MapFollowEndpoints();
app.MapAttachmentEndpoints();
app.MapSearchEndpoints();
app.MapStatusEndpoints();
app.MapChatEndpoints();
app.MapReviewEndpoints();
app.MapWalletEndpoints();
app.MapVoucherEndpoints();
app.MapWishlistEndpoints();
app.MapTotpEndpoints();
app.MapVendorStatsEndpoints();

// Fallback to index.html for SPA routing
app.MapFallbackToFile("index.html");

app.Run();
