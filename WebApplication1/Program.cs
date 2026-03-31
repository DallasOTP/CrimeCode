using System.Text;
using System.Threading.RateLimiting;
using CrimeCode.Data;
using CrimeCode.Endpoints;
using CrimeCode.Middleware;
using CrimeCode.Models;
using CrimeCode.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Hide server identity from responses
builder.WebHost.ConfigureKestrel(opt => opt.AddServerHeader = false);

// Database
builder.Services.AddDbContext<CrimeCodeDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=crimecode.db"));

// CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(
            builder.Configuration.GetSection("Cors:Origins").Get<string[]>() 
            ?? ["https://crimecode-production.up.railway.app"]
        )
        .AllowAnyHeader()
        .AllowAnyMethod();
    });
});

// Rate Limiting — multi-tier with sliding windows and per-IP partitioning
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;

    // Global: 200 req/min per IP (sliding window for smoother throttling)
    options.AddSlidingWindowLimiter("global", opt =>
    {
        opt.PermitLimit = 200;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.SegmentsPerWindow = 4;
        opt.QueueLimit = 0;
    });

    // Auth endpoints: 8 req/5min per IP (brute-force protection)
    options.AddSlidingWindowLimiter("auth", opt =>
    {
        opt.PermitLimit = 8;
        opt.Window = TimeSpan.FromMinutes(5);
        opt.SegmentsPerWindow = 5;
        opt.QueueLimit = 0;
    });

    // API writes (POST/PUT/DELETE): 30 req/min per IP
    options.AddSlidingWindowLimiter("api-write", opt =>
    {
        opt.PermitLimit = 30;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.SegmentsPerWindow = 4;
        opt.QueueLimit = 0;
    });

    // Search/listing: 60 req/min per IP (anti-scraping)
    options.AddSlidingWindowLimiter("api-read", opt =>
    {
        opt.PermitLimit = 60;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.SegmentsPerWindow = 4;
        opt.QueueLimit = 0;
    });

    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.Headers["Retry-After"] = "10";
        await context.HttpContext.Response.WriteAsJsonAsync(
            new { error = "Rate limit exceeded. Try again later.", retryAfter = 10 }, token);
    };
});

// Forwarded headers (for Railway / reverse proxy — correct client IP)
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

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

// Ensure DB schema is created — force reset if schema version mismatch
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CrimeCodeDbContext>();
    var dbPath = db.Database.GetConnectionString()?.Replace("Data Source=", "") ?? "crimecode.db";
    var versionFile = Path.Combine(Path.GetDirectoryName(dbPath) ?? ".", ".db_version");
    const string currentVersion = "v3_marketplace_only";
    var needsReset = !File.Exists(versionFile) || File.ReadAllText(versionFile).Trim() != currentVersion;
    if (needsReset)
    {
        db.Database.EnsureDeleted();
        db.Database.EnsureCreated();
        File.WriteAllText(versionFile, currentVersion);
    }
    else
    {
        db.Database.EnsureCreated();
    }
}

// Healthcheck endpoint — responds immediately, before any middleware
app.MapGet("/healthz", () => Results.Ok("ok"));

// --- Security Pipeline ---
// 1. Forwarded headers (must be first — fixes client IP for proxied requests)
app.UseForwardedHeaders();

// 2. DDoS protection — IP tracking, burst detection, auto-ban
app.UseMiddleware<DdosProtectionMiddleware>();

// 3. Request validation — blocks injection, path traversal, oversized bodies
app.UseMiddleware<RequestValidationMiddleware>();

// 4. Security headers — CSP, HSTS, X-Frame-Options, anti-sniff, anti-fingerprint
app.UseMiddleware<SecurityHeadersMiddleware>();

// 5. Serve static files (wwwroot)
app.UseStaticFiles();

app.UseCors();
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

// Map API endpoints
app.MapAuthEndpoints();
app.MapTelegramAuthEndpoints();
app.MapCategoryEndpoints();
app.MapUserEndpoints();
app.MapAdminEndpoints();
app.MapAvatarEndpoints();
app.MapNotificationEndpoints();
app.MapMessageEndpoints();
app.MapReputationEndpoints();
app.MapMarketplaceEndpoints();
app.MapVendorEndpoints();
app.MapOrderEndpoints();
app.MapLeaderboardEndpoints();
app.MapFollowEndpoints();
app.MapStatusEndpoints();
app.MapChatEndpoints();
app.MapReviewEndpoints();
app.MapWalletEndpoints();
app.MapVoucherEndpoints();
app.MapWishlistEndpoints();
app.MapTotpEndpoints();
app.MapVendorStatsEndpoints();
app.MapTicketEndpoints();
app.MapAnalyticsEndpoints();
app.MapExportEndpoints();

// Fallback to index.html for SPA routing
app.MapFallbackToFile("index.html");

// Seed DB in background AFTER the server starts listening (so healthcheck passes immediately)
app.Lifetime.ApplicationStarted.Register(() =>
{
    _ = Task.Run(() =>
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CrimeCodeDbContext>();
        db.Database.EnsureCreated();

        if (!db.Users.Any())
        {
            var now = DateTime.UtcNow;
            var hash = BCrypt.Net.BCrypt.HashPassword("admin123");

            var admin = new User { Username = "Admin", Email = "admin@crimecode.it", PasswordHash = hash, Role = "Admin", CreatedAt = now.AddDays(-90), ReputationScore = 50, Credits = 999 };
            var v1 = new User { Username = "CyberVendor", Email = "cyber@vendor.cc", PasswordHash = hash, Role = "User", IsVendor = true, VendorApprovedAt = now.AddDays(-60), VendorBio = "Specialista in tool di sicurezza e guide premium", CreatedAt = now.AddDays(-60), ReputationScore = 35, Credits = 500 };
            var v2 = new User { Username = "DataShark", Email = "shark@vendor.cc", PasswordHash = hash, Role = "User", IsVendor = true, VendorApprovedAt = now.AddDays(-45), VendorBio = "Database, risorse OSINT e servizi custom", CreatedAt = now.AddDays(-45), ReputationScore = 28, Credits = 300 };
            var v3 = new User { Username = "GhostDev", Email = "ghost@vendor.cc", PasswordHash = hash, Role = "User", IsVendor = true, VendorApprovedAt = now.AddDays(-30), VendorBio = "Sviluppatore full-stack, bot custom e automazioni", CreatedAt = now.AddDays(-30), ReputationScore = 20, Credits = 150 };
            var u1 = new User { Username = "H4cker_N00b", Email = "noob@user.cc", PasswordHash = hash, Role = "User", CreatedAt = now.AddDays(-20), ReputationScore = 5, Credits = 50 };
            var u2 = new User { Username = "ShadowByte", Email = "shadow@user.cc", PasswordHash = hash, Role = "User", CreatedAt = now.AddDays(-15), ReputationScore = 3, Credits = 30 };
            db.Users.AddRange(admin, v1, v2, v3, u1, u2);
            db.SaveChanges();

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

            var orders = new List<MarketplaceOrder>
            {
                new() { ListingId = listings[0].Id, BuyerId = u1.Id, SellerId = v1.Id, Quantity = 1, Amount = listings[0].PriceCrypto, Currency = "BTC", Status = "Completed", EscrowStatus = "Released", DeliveryType = "Instant", CreatedAt = now.AddDays(-30) },
                new() { ListingId = listings[1].Id, BuyerId = u2.Id, SellerId = v2.Id, Quantity = 1, Amount = listings[1].PriceCrypto, Currency = "BTC", Status = "Completed", EscrowStatus = "Released", DeliveryType = "Instant", CreatedAt = now.AddDays(-20) },
                new() { ListingId = listings[4].Id, BuyerId = u1.Id, SellerId = v2.Id, Quantity = 1, Amount = listings[4].PriceCrypto, Currency = "USDT", Status = "Completed", EscrowStatus = "Released", DeliveryType = "Instant", CreatedAt = now.AddDays(-15) },
            };
            db.MarketplaceOrders.AddRange(orders);
            db.SaveChanges();

            db.VendorReviews.AddRange(
                new VendorReview { OrderId = orders[0].Id, BuyerId = u1.Id, SellerId = v1.Id, Rating = 5, Comment = "Guida eccellente, molto dettagliata. Consigliato!", CreatedAt = now.AddDays(-29) },
                new VendorReview { OrderId = orders[1].Id, BuyerId = u2.Id, SellerId = v2.Id, Rating = 4, Comment = "Tool funzionante, buon supporto.", CreatedAt = now.AddDays(-19) },
                new VendorReview { OrderId = orders[2].Id, BuyerId = u1.Id, SellerId = v2.Id, Rating = 5, Comment = "Raccolta vastissima, vale ogni satoshi!", CreatedAt = now.AddDays(-14) }
            );
            db.SaveChanges();
        }
    });
});

app.Run();
