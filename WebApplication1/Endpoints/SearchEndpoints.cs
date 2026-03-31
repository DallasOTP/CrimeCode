using CrimeCode.Data;
using CrimeCode.DTOs;
using Microsoft.EntityFrameworkCore;

namespace CrimeCode.Endpoints;

public static class SearchEndpoints
{
    public static void MapSearchEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/search");

        group.MapPost("/advanced", async (AdvancedSearchRequest req, CrimeCodeDbContext db) =>
        {
            var query = db.Threads
                .Include(t => t.Author)
                .Include(t => t.Category)
                .Include(t => t.Posts)
                .Include(t => t.Tag)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(req.Query))
            {
                var searchTerm = req.Query.ToLower();
                query = query.Where(t => t.Title.ToLower().Contains(searchTerm) || t.Posts.Any(p => p.Content.ToLower().Contains(searchTerm)));
            }

            if (req.CategoryId.HasValue)
                query = query.Where(t => t.CategoryId == req.CategoryId.Value);

            if (req.AuthorId.HasValue)
                query = query.Where(t => t.AuthorId == req.AuthorId.Value);

            if (req.TagId.HasValue)
                query = query.Where(t => t.TagId == req.TagId.Value);

            if (!string.IsNullOrWhiteSpace(req.DateFrom) && DateTime.TryParse(req.DateFrom, out var dateFrom))
                query = query.Where(t => t.CreatedAt >= dateFrom);

            if (!string.IsNullOrWhiteSpace(req.DateTo) && DateTime.TryParse(req.DateTo, out var dateTo))
                query = query.Where(t => t.CreatedAt <= dateTo.AddDays(1));

            var total = await query.CountAsync();

            var page = Math.Max(1, req.Page);
            var pageSize = Math.Clamp(req.PageSize, 1, 50);

            var threads = await query
                .OrderByDescending(t => t.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(t => new ThreadSummaryDto(
                    t.Id, t.Title, t.Prefix, t.Author.Username, t.AuthorId, t.Author.AvatarUrl,
                    t.Category.Name, t.Posts.Count, t.ViewCount, t.IsPinned, t.IsLocked,
                    t.CreatedAt, t.LastActivityAt,
                    t.Tag != null ? t.Tag.Name : null, t.Tag != null ? t.Tag.Color : null,
                    t.Posts.OrderByDescending(p => p.CreatedAt).Select(p => p.Author.Username).FirstOrDefault(),
                    t.Posts.OrderByDescending(p => p.CreatedAt).Select(p => (DateTime?)p.CreatedAt).FirstOrDefault()
                ))
                .ToListAsync();

            return Results.Ok(new { Total = total, Page = page, PageSize = pageSize, TotalPages = (int)Math.Ceiling((double)total / pageSize), Threads = threads });
        });
    }
}
