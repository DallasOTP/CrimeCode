namespace CrimeCode.Models;

public class CryptoWallet
{
    public int Id { get; set; }
    public decimal Balance { get; set; } = 0;
    public string Currency { get; set; } = "USDT"; // BTC, ETH, USDT, LTC, XMR
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public int UserId { get; set; }
    public User User { get; set; } = null!;
}

public class WalletTransaction
{
    public int Id { get; set; }
    public decimal Amount { get; set; }
    public string Type { get; set; } = "Deposit"; // Deposit, Withdraw, EscrowLock, EscrowRelease, EscrowRefund, Purchase
    public string? Reference { get; set; } // order id, tx hash, etc
    public string Currency { get; set; } = "USDT";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int WalletId { get; set; }
    public CryptoWallet Wallet { get; set; } = null!;

    public int UserId { get; set; }
    public User User { get; set; } = null!;
}
