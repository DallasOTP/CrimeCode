using System.Security.Claims;
using CrimeCode.Data;
using CrimeCode.DTOs;
using CrimeCode.Models;
using Microsoft.EntityFrameworkCore;

namespace CrimeCode.Endpoints;

public static class WalletEndpoints
{
    public static void MapWalletEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/wallet").RequireAuthorization();

        // Get my wallets
        group.MapGet("/", async (ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            var userId = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var wallets = await db.CryptoWallets
                .Where(w => w.UserId == userId)
                .Select(w => new WalletDto(w.Id, w.Currency, w.Balance, w.UpdatedAt))
                .ToListAsync();

            return Results.Ok(wallets);
        });

        // Initialize wallet for a currency
        group.MapPost("/init", async (string currency, ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            var userId = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var validCurrencies = new[] { "BTC", "ETH", "USDT", "LTC", "XMR" };
            if (!validCurrencies.Contains(currency))
                return Results.BadRequest(new { error = "Valuta non valida" });

            var existing = await db.CryptoWallets.AnyAsync(w => w.UserId == userId && w.Currency == currency);
            if (existing) return Results.BadRequest(new { error = "Wallet già esistente per questa valuta" });

            var wallet = new CryptoWallet
            {
                UserId = userId,
                Currency = currency,
                Balance = 0
            };
            db.CryptoWallets.Add(wallet);
            await db.SaveChangesAsync();

            return Results.Created($"/api/wallet/{wallet.Id}", new WalletDto(wallet.Id, wallet.Currency, wallet.Balance, wallet.UpdatedAt));
        });

        // Deposit (simulated — in production, verify on-chain)
        group.MapPost("/deposit", async (DepositRequest req, ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            var userId = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);

            if (req.Amount <= 0) return Results.BadRequest(new { error = "Importo non valido" });
            if (string.IsNullOrWhiteSpace(req.TxId)) return Results.BadRequest(new { error = "TX ID obbligatorio" });

            var wallet = await db.CryptoWallets.FirstOrDefaultAsync(w => w.UserId == userId && w.Currency == req.Currency);
            if (wallet is null) return Results.NotFound(new { error = "Wallet non trovato. Inizializzalo prima." });

            wallet.Balance += req.Amount;
            wallet.UpdatedAt = DateTime.UtcNow;

            db.WalletTransactions.Add(new WalletTransaction
            {
                WalletId = wallet.Id,
                UserId = userId,
                Amount = req.Amount,
                Type = "Deposit",
                Currency = req.Currency,
                Reference = req.TxId
            });

            await db.SaveChangesAsync();
            return Results.Ok(new { balance = wallet.Balance, currency = wallet.Currency });
        });

        // Withdraw (simulated)
        group.MapPost("/withdraw", async (WithdrawRequest req, ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            var userId = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);

            if (req.Amount <= 0) return Results.BadRequest(new { error = "Importo non valido" });
            if (string.IsNullOrWhiteSpace(req.WalletAddress)) return Results.BadRequest(new { error = "Indirizzo wallet obbligatorio" });

            var wallet = await db.CryptoWallets.FirstOrDefaultAsync(w => w.UserId == userId && w.Currency == req.Currency);
            if (wallet is null) return Results.NotFound(new { error = "Wallet non trovato" });
            if (wallet.Balance < req.Amount) return Results.BadRequest(new { error = "Saldo insufficiente" });

            wallet.Balance -= req.Amount;
            wallet.UpdatedAt = DateTime.UtcNow;

            db.WalletTransactions.Add(new WalletTransaction
            {
                WalletId = wallet.Id,
                UserId = userId,
                Amount = -req.Amount,
                Type = "Withdraw",
                Currency = req.Currency,
                Reference = $"TO:{req.WalletAddress}"
            });

            await db.SaveChangesAsync();
            return Results.Ok(new { balance = wallet.Balance, currency = wallet.Currency });
        });

        // Transaction history
        group.MapGet("/transactions", async (string? currency, int? page, ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            var userId = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var p = Math.Max(page ?? 1, 1);

            var query = db.WalletTransactions.Where(t => t.UserId == userId);
            if (!string.IsNullOrWhiteSpace(currency))
                query = query.Where(t => t.Currency == currency);

            var total = await query.CountAsync();
            var txs = await query
                .OrderByDescending(t => t.CreatedAt)
                .Skip((p - 1) * 20)
                .Take(20)
                .Select(t => new WalletTransactionDto(t.Id, t.Amount, t.Type, t.Reference, t.Currency, t.CreatedAt))
                .ToListAsync();

            return Results.Ok(new { transactions = txs, total, page = p });
        });
    }
}
