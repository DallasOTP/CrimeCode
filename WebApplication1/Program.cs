using System.Text;
using CrimeCode.Data;
using CrimeCode.Endpoints;
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
