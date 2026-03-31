using System.Security.Claims;
using CrimeCode.Data;
using CrimeCode.DTOs;
using CrimeCode.Models;
using CrimeCode.Services;
using Microsoft.EntityFrameworkCore;

namespace CrimeCode.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/auth");

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

        group.MapPost("/login", async (LoginRequest req, CrimeCodeDbContext db, AuthService auth) =>
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == req.Email);
            if (user is null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
                return Results.Unauthorized();

            var token = auth.GenerateToken(user.Id, user.Username, user.Role);
            return Results.Ok(new AuthResponse(user.Id, user.Username, token));
        });

        group.MapGet("/me", async (ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            var userIdStr = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdStr is null || !int.TryParse(userIdStr, out var userId))
                return Results.Unauthorized();

            var user = await db.Users
                .Include(u => u.Threads)
                .Include(u => u.Posts)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user is null) return Results.NotFound();

            // Update LastSeenAt
            user.LastSeenAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            var ranks = await db.UserRanks.OrderByDescending(r => r.MinPosts).ToListAsync();
            var rank = LeaderboardEndpoints.GetRank(user, ranks);

            return Results.Ok(new UserProfile(user.Id, user.Username, user.AvatarUrl, user.Bio, user.Signature,
                user.Role, user.CustomTitle, user.CreatedAt, user.LastSeenAt,
                user.Threads.Count, user.Posts.Count, user.Credits, user.ReputationScore,
                rank.Name, rank.Color, rank.Icon));
        }).RequireAuthorization();
    }
}
