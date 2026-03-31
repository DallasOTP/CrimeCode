using System.Security.Claims;
using CrimeCode.Data;
using CrimeCode.Models;
using Microsoft.EntityFrameworkCore;

namespace CrimeCode.Endpoints;

public static class AttachmentEndpoints
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".webp", ".pdf", ".txt", ".zip", ".rar"
    };

    private const long MaxFileSize = 5 * 1024 * 1024; // 5MB

    public static void MapAttachmentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/posts/{postId:int}/attachments");

        // Upload attachment
        group.MapPost("/", async (int postId, IFormFile file, ClaimsPrincipal principal, CrimeCodeDbContext db, IWebHostEnvironment env) =>
        {
            var userId = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var post = await db.Posts.FindAsync(postId);
            if (post is null) return Results.NotFound();

            if (file.Length > MaxFileSize)
                return Results.BadRequest("File troppo grande (max 5MB).");

            var ext = Path.GetExtension(file.FileName);
            if (!AllowedExtensions.Contains(ext))
                return Results.BadRequest("Tipo di file non consentito.");

            var existingCount = await db.PostAttachments.CountAsync(a => a.PostId == postId);
            if (existingCount >= 5)
                return Results.BadRequest("Massimo 5 allegati per post.");

            var uploadsDir = Path.Combine(env.ContentRootPath, "wwwroot", "uploads");
            Directory.CreateDirectory(uploadsDir);

            var storedName = $"{Guid.NewGuid()}{ext}";
            var storedPath = Path.Combine(uploadsDir, storedName);

            await using var stream = new FileStream(storedPath, FileMode.Create);
            await file.CopyToAsync(stream);

            var attachment = new PostAttachment
            {
                FileName = file.FileName,
                StoredPath = $"/uploads/{storedName}",
                ContentType = file.ContentType,
                FileSizeBytes = file.Length,
                PostId = postId,
                UploaderId = userId,
                CreatedAt = DateTime.UtcNow
            };

            db.PostAttachments.Add(attachment);
            await db.SaveChangesAsync();

            return Results.Ok(new { attachment.Id, attachment.FileName, Url = attachment.StoredPath, attachment.ContentType, attachment.FileSizeBytes });
        }).RequireAuthorization().DisableAntiforgery();

        // Delete attachment
        group.MapDelete("/{attachmentId:int}", async (int postId, int attachmentId, ClaimsPrincipal principal, CrimeCodeDbContext db, IWebHostEnvironment env) =>
        {
            var userId = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var attachment = await db.PostAttachments.FirstOrDefaultAsync(a => a.Id == attachmentId && a.PostId == postId);
            if (attachment is null) return Results.NotFound();

            var user = await db.Users.FindAsync(userId);
            if (attachment.UploaderId != userId && user?.Role != "admin")
                return Results.Forbid();

            var filePath = Path.Combine(env.ContentRootPath, "wwwroot", attachment.StoredPath.TrimStart('/'));
            if (File.Exists(filePath)) File.Delete(filePath);

            db.PostAttachments.Remove(attachment);
            await db.SaveChangesAsync();
            return Results.Ok();
        }).RequireAuthorization();
    }
}
