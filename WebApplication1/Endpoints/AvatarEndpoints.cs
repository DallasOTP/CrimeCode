using System.Security.Claims;
using CrimeCode.Data;
using Microsoft.EntityFrameworkCore;

namespace CrimeCode.Endpoints;

public static class AvatarEndpoints
{
    private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".gif", ".webp"];
    private const long MaxFileSize = 2 * 1024 * 1024; // 2 MB

    public static void MapAvatarEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/avatar").RequireAuthorization();

        group.MapPost("/upload", async (HttpRequest request, ClaimsPrincipal principal, CrimeCodeDbContext db, IWebHostEnvironment env) =>
        {
            var userId = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);

            if (!request.HasFormContentType)
                return Results.BadRequest(new { error = "Content-Type deve essere multipart/form-data" });

            var form = await request.ReadFormAsync();
            var file = form.Files.GetFile("avatar");

            if (file is null || file.Length == 0)
                return Results.BadRequest(new { error = "Nessun file caricato" });

            if (file.Length > MaxFileSize)
                return Results.BadRequest(new { error = "Il file non può superare 2 MB" });

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(ext))
                return Results.BadRequest(new { error = "Formato non supportato. Usa: jpg, png, gif, webp" });

            // Create uploads directory
            var uploadsDir = Path.Combine(env.WebRootPath, "uploads", "avatars");
            Directory.CreateDirectory(uploadsDir);

            // Delete old avatar if exists
            var user = await db.Users.FindAsync(userId);
            if (user is null) return Results.NotFound();

            if (!string.IsNullOrEmpty(user.AvatarUrl))
            {
                var oldPath = Path.Combine(env.WebRootPath, user.AvatarUrl.TrimStart('/'));
                if (File.Exists(oldPath)) File.Delete(oldPath);
            }

            // Save new avatar with unique name
            var fileName = $"{userId}_{Guid.NewGuid():N}{ext}";
            var filePath = Path.Combine(uploadsDir, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            user.AvatarUrl = $"/uploads/avatars/{fileName}";
            await db.SaveChangesAsync();

            return Results.Ok(new { avatarUrl = user.AvatarUrl });
        }).DisableAntiforgery();

        group.MapDelete("/", async (ClaimsPrincipal principal, CrimeCodeDbContext db, IWebHostEnvironment env) =>
        {
            var userId = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var user = await db.Users.FindAsync(userId);
            if (user is null) return Results.NotFound();

            if (!string.IsNullOrEmpty(user.AvatarUrl))
            {
                var oldPath = Path.Combine(env.WebRootPath, user.AvatarUrl.TrimStart('/'));
                if (File.Exists(oldPath)) File.Delete(oldPath);
            }

            user.AvatarUrl = null;
            await db.SaveChangesAsync();

            return Results.Ok(new { message = "Avatar rimosso" });
        });
    }
}
