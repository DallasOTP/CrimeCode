using CrimeCode.Data;
using CrimeCode.DTOs;
using Microsoft.EntityFrameworkCore;

namespace CrimeCode.Endpoints;

public static class CategoryEndpoints
{
    public static void MapCategoryEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/categories");

        group.MapGet("/", async (CrimeCodeDbContext db) =>
        {
            var allCategories = await db.Categories
                .Include(c => c.SubCategories)
                .OrderBy(c => c.SortOrder)
                .ToListAsync();

            var topLevel = allCategories.Where(c => c.ParentId == null).ToList();

            var result = topLevel.Select(c => new CategoryDto(
                c.Id, c.Name, c.Description, c.Icon,
                c.ParentId, c.IsMarketplace,
                c.SubCategories.OrderBy(sc => sc.SortOrder).Select(sc => new CategoryDto(
                    sc.Id, sc.Name, sc.Description, sc.Icon,
                    sc.ParentId, sc.IsMarketplace, null
                )).ToList()
            )).ToList();

            return Results.Ok(result);
        });
    }
}
