using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Web;
using CrimeCode.Data;
using CrimeCode.DTOs;
using CrimeCode.Models;
using CrimeCode.Services;
using Microsoft.EntityFrameworkCore;

namespace CrimeCode.Endpoints;

public static class TelegramAuthEndpoints
{
    public static void MapTelegramAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/auth");

        group.MapPost("/telegram", async (TelegramLoginRequest req, CrimeCodeDbContext db, AuthService auth, IConfiguration config) =>
        {
            var botToken = config["Telegram:BotToken"];
            if (string.IsNullOrWhiteSpace(botToken) || botToken == "YOUR_BOT_TOKEN_HERE")
                return Results.StatusCode(503);

            // Parse initData
            var parsed = HttpUtility.ParseQueryString(req.InitData);
            var hash = parsed["hash"];
            if (string.IsNullOrEmpty(hash))
                return Results.BadRequest(new { error = "Hash mancante in initData" });

            // Build data-check-string (sorted key=value pairs, excluding hash)
            var dataCheckParts = parsed.AllKeys
                .Where(k => k != "hash" && k is not null)
                .OrderBy(k => k, StringComparer.Ordinal)
                .Select(k => $"{k}={parsed[k]}")
                .ToList();
            var dataCheckString = string.Join("\n", dataCheckParts);

            // HMAC-SHA256 validation per Telegram docs
            var secretKey = HMACSHA256.HashData(Encoding.UTF8.GetBytes("WebAppData"), Encoding.UTF8.GetBytes(botToken));
            var computedHash = HMACSHA256.HashData(secretKey, Encoding.UTF8.GetBytes(dataCheckString));
            var computedHashHex = Convert.ToHexStringLower(computedHash);

            if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(computedHashHex),
                    Encoding.UTF8.GetBytes(hash)))
                return Results.Unauthorized();

            // Check auth_date freshness (max 5 minutes)
            if (parsed["auth_date"] is string authDateStr && long.TryParse(authDateStr, out var authDateUnix))
            {
                var authDate = DateTimeOffset.FromUnixTimeSeconds(authDateUnix).UtcDateTime;
                if (DateTime.UtcNow - authDate > TimeSpan.FromMinutes(5))
                    return Results.BadRequest(new { error = "initData scaduto" });
            }

            // Extract user info from initData
            var userJson = parsed["user"];
            if (string.IsNullOrEmpty(userJson))
                return Results.BadRequest(new { error = "Dati utente Telegram mancanti" });

            TelegramUserData? tgUser;
            try
            {
                tgUser = JsonSerializer.Deserialize<TelegramUserData>(userJson);
            }
            catch
            {
                return Results.BadRequest(new { error = "Dati utente Telegram non validi" });
            }

            if (tgUser is null || tgUser.Id == 0)
                return Results.BadRequest(new { error = "ID utente Telegram non valido" });

            // Find or create user
            var user = await db.Users.FirstOrDefaultAsync(u => u.TelegramId == tgUser.Id);
            if (user is null)
            {
                var username = !string.IsNullOrWhiteSpace(tgUser.Username)
                    ? tgUser.Username
                    : $"tg_{tgUser.Id}";

                // Ensure unique username
                var baseUsername = username;
                var counter = 1;
                while (await db.Users.AnyAsync(u => u.Username == username))
                {
                    username = $"{baseUsername}_{counter++}";
                }

                user = new User
                {
                    Username = username,
                    Email = $"tg_{tgUser.Id}@telegram.local",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString()),
                    TelegramId = tgUser.Id,
                    AvatarUrl = tgUser.PhotoUrl
                };
                db.Users.Add(user);
                await db.SaveChangesAsync();
            }
            else
            {
                // Update avatar if changed
                if (!string.IsNullOrWhiteSpace(tgUser.PhotoUrl))
                    user.AvatarUrl = tgUser.PhotoUrl;
                user.LastSeenAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
            }

            var token = auth.GenerateToken(user.Id, user.Username, user.Role);
            return Results.Ok(new AuthResponse(user.Id, user.Username, token));
        });
    }

    private record TelegramUserData
    {
        public long Id { get; init; }
        public string? FirstName { get; init; }
        public string? LastName { get; init; }
        public string? Username { get; init; }
        public string? PhotoUrl { get; init; }
    }
}
