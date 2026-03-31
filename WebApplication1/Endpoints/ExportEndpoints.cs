using System.Security.Claims;
using System.Text;
using CrimeCode.Data;
using Microsoft.EntityFrameworkCore;

namespace CrimeCode.Endpoints;

public static class ExportEndpoints
{
    public static void MapExportEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/export").RequireAuthorization();

        // Export my orders as CSV
        group.MapGet("/orders", async (string type, ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            var userId = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var query = db.MarketplaceOrders
                .Include(o => o.Listing)
                .Include(o => o.Buyer)
                .Include(o => o.Seller)
                .AsQueryable();

            if (type == "buying")
                query = query.Where(o => o.BuyerId == userId);
            else if (type == "selling")
                query = query.Where(o => o.SellerId == userId);
            else
                return Results.BadRequest(new { error = "type deve essere 'buying' o 'selling'" });

            var orders = await query.OrderByDescending(o => o.CreatedAt).ToListAsync();

            var sb = new StringBuilder();
            sb.AppendLine("ID,Prodotto,Quantità,Importo,Valuta,Stato,Escrow,Tipo Consegna,Data Creazione,Data Aggiornamento");
            foreach (var o in orders)
            {
                sb.AppendLine($"{o.Id},\"{EscapeCsv(o.Listing.Title)}\",{o.Quantity},{o.Amount},{o.Currency},{o.Status},{o.EscrowStatus},{o.DeliveryType},{o.CreatedAt:yyyy-MM-dd HH:mm},{o.UpdatedAt?.ToString("yyyy-MM-dd HH:mm") ?? ""}");
            }

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            return Results.File(bytes, "text/csv", $"orders_{type}_{DateTime.UtcNow:yyyyMMdd}.csv");
        });
    }

    private static string EscapeCsv(string value)
    {
        return value.Replace("\"", "\"\"");
    }
}
