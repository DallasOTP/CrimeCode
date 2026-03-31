using System.Collections.Concurrent;
using System.Security.Claims;
using CrimeCode.Data;
using CrimeCode.DTOs;
using CrimeCode.Models;
using CrimeCode.Services;
using Microsoft.EntityFrameworkCore;

namespace CrimeCode.Endpoints;

public static class AuthEndpoints
{
    // Anti-bruteforce: track failed login attempts per email
    private static readonly ConcurrentDictionary<string, (int Count, DateTime LastAttempt)> _failedLogins = new();
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/auth").RequireRateLimiting("auth");

        group.MapPost("/register", async (RegisterRequest req, CrimeCodeDbContext db, AuthService auth) =>
        {
            if (string.IsNullOrWhiteSpace(req.Username) || req.Username.Length < 3)
                return Results.BadRequest(new { error = "Username deve avere almeno 3 caratteri" });

            if (string.IsNullOrWhiteSpace(req.Email) || !req.Email.Contains('@'))
                return Results.BadRequest(new { error = "Email non valida" });

            if (string.IsNullOrWhiteSpace(req.Password) || req.Password.Length < 6)
                return Results.BadRequest(new { error = "Password deve avere almeno 6 caratteri" });

            if (await db.Users.AnyAsync(u => u.Email == req.Email))
                return Results.Conflict(new { error = "Email già registrata" });

            if (await db.Users.AnyAsync(u => u.Username == req.Username))
                return Results.Conflict(new { error = "Username già in uso" });

            var user = new User
            {
                Username = req.Username,
                Email = req.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password)
            };

            db.Users.Add(user);
            await db.SaveChangesAsync();

            var token = auth.GenerateToken(user.Id, user.Username, user.Role);
            return Results.Ok(new AuthResponse(user.Id, user.Username, token));
        });

        group.MapPost("/login", async (LoginWith2FARequest req, CrimeCodeDbContext db, AuthService auth) =>
        {
            // Anti-bruteforce check
            var key = (req.Email ?? "").ToLowerInvariant();
            if (_failedLogins.TryGetValue(key, out var record) && record.Count >= MaxFailedAttempts 
                && DateTime.UtcNow - record.LastAttempt < LockoutDuration)
            {
                return Results.Json(new { error = "Troppi tentativi falliti. Riprova tra 15 minuti." }, statusCode: 429);
            }

            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == req.Email);
            if (user is null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            {
                // Track failed attempt
                _failedLogins.AddOrUpdate(key, 
                    _ => (1, DateTime.UtcNow),
                    (_, old) => (old.Count + 1, DateTime.UtcNow));
                return Results.Unauthorized();
            }

            // 2FA check
            if (user.Is2FAEnabled)
            {
                if (string.IsNullOrWhiteSpace(req.TotpCode))
                    return Results.Json(new { requires2FA = true, error = "Inserisci il codice 2FA" }, statusCode: 401);

                if (!TotpEndpoints.VerifyTotp(user.TotpSecret!, req.TotpCode))
                    return Results.Json(new { requires2FA = true, error = "Codice 2FA non valido" }, statusCode: 401);
            }

            // Reset failed attempts on successful login
            _failedLogins.TryRemove(key, out _);

            var token = auth.GenerateToken(user.Id, user.Username, user.Role);
            return Results.Ok(new AuthResponse(user.Id, user.Username, token));
        });

        group.MapGet("/me", async (ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            var userIdStr = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdStr is null || !int.TryParse(userIdStr, out var userId))
                return Results.Unauthorized();

            var user = await db.Users
                .Include(u => u.Followers)
                .Include(u => u.Following)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user is null) return Results.NotFound();

            // Update LastSeenAt
            user.LastSeenAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            return Results.Ok(new UserProfile(user.Id, user.Username, user.AvatarUrl, user.Bio, user.Signature,
                user.Role, user.CustomTitle, user.CreatedAt, user.LastSeenAt,
                user.Credits, user.ReputationScore, user.Status,
                user.Followers.Count, user.Following.Count, false,
                user.BannerUrl, user.Website, user.Location, user.Jabber, user.Birthday));
        }).RequireAuthorization().RequireRateLimiting("api-read");
    }
}
