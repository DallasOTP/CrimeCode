using System.Security.Claims;
using System.Security.Cryptography;
using CrimeCode.Data;
using CrimeCode.DTOs;
using Microsoft.EntityFrameworkCore;

namespace CrimeCode.Endpoints;

public static class TotpEndpoints
{
    public static void MapTotpEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/2fa").RequireAuthorization();

        // Setup 2FA — generate secret
        group.MapPost("/setup", async (ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            var userId = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var user = await db.Users.FindAsync(userId);
            if (user is null) return Results.NotFound();
            if (user.Is2FAEnabled)
                return Results.BadRequest(new { error = "2FA già abilitato" });

            var secret = GenerateSecret();
            user.TotpSecret = secret;
            await db.SaveChangesAsync();

            var uri = $"otpauth://totp/CrimeCode:{user.Username}?secret={secret}&issuer=CrimeCode&digits=6&period=30";
            return Results.Ok(new TotpSetupResponse(secret, uri));
        });

        // Enable 2FA — verify code and activate
        group.MapPost("/enable", async (TotpVerifyRequest req, ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            var userId = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var user = await db.Users.FindAsync(userId);
            if (user is null) return Results.NotFound();
            if (user.Is2FAEnabled) return Results.BadRequest(new { error = "2FA già abilitato" });
            if (string.IsNullOrWhiteSpace(user.TotpSecret))
                return Results.BadRequest(new { error = "Genera prima il secret con /setup" });

            if (!VerifyTotp(user.TotpSecret, req.Code))
                return Results.BadRequest(new { error = "Codice non valido" });

            user.Is2FAEnabled = true;
            await db.SaveChangesAsync();
            return Results.Ok(new { enabled = true });
        });

        // Disable 2FA
        group.MapPost("/disable", async (TotpVerifyRequest req, ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            var userId = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var user = await db.Users.FindAsync(userId);
            if (user is null) return Results.NotFound();
            if (!user.Is2FAEnabled) return Results.BadRequest(new { error = "2FA non abilitato" });

            if (!VerifyTotp(user.TotpSecret!, req.Code))
                return Results.BadRequest(new { error = "Codice non valido" });

            user.Is2FAEnabled = false;
            user.TotpSecret = null;
            await db.SaveChangesAsync();
            return Results.Ok(new { enabled = false });
        });

        // Status
        group.MapGet("/status", async (ClaimsPrincipal principal, CrimeCodeDbContext db) =>
        {
            var userId = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var user = await db.Users.FindAsync(userId);
            return Results.Ok(new { enabled = user?.Is2FAEnabled ?? false });
        });
    }

    // --- TOTP Implementation (RFC 6238 / RFC 4226) ---

    private static string GenerateSecret()
    {
        var bytes = RandomNumberGenerator.GetBytes(20);
        return Base32Encode(bytes);
    }

    public static bool VerifyTotp(string secret, string code, int toleranceSteps = 1)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length != 6) return false;

        var keyBytes = Base32Decode(secret);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30;

        for (int i = -toleranceSteps; i <= toleranceSteps; i++)
        {
            var step = now + i;
            var stepBytes = BitConverter.GetBytes(step);
            if (BitConverter.IsLittleEndian) Array.Reverse(stepBytes);
            var hash = HMACSHA1.HashData(keyBytes, stepBytes);

            var offset = hash[^1] & 0x0F;
            var otp = ((hash[offset] & 0x7F) << 24) |
                      ((hash[offset + 1] & 0xFF) << 16) |
                      ((hash[offset + 2] & 0xFF) << 8) |
                      (hash[offset + 3] & 0xFF);

            var generated = (otp % 1_000_000).ToString("D6");
            if (CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(generated),
                System.Text.Encoding.UTF8.GetBytes(code)))
                return true;
        }
        return false;
    }

    private static string Base32Encode(byte[] data)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var result = new System.Text.StringBuilder();
        int buffer = 0, bitsLeft = 0;
        foreach (var b in data)
        {
            buffer = (buffer << 8) | b;
            bitsLeft += 8;
            while (bitsLeft >= 5)
            {
                result.Append(alphabet[(buffer >> (bitsLeft - 5)) & 0x1F]);
                bitsLeft -= 5;
            }
        }
        if (bitsLeft > 0)
            result.Append(alphabet[(buffer << (5 - bitsLeft)) & 0x1F]);
        return result.ToString();
    }

    private static byte[] Base32Decode(string input)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        input = input.TrimEnd('=').ToUpperInvariant();
        var output = new List<byte>();
        int buffer = 0, bitsLeft = 0;
        foreach (var c in input)
        {
            var val = alphabet.IndexOf(c);
            if (val < 0) continue;
            buffer = (buffer << 5) | val;
            bitsLeft += 5;
            if (bitsLeft >= 8)
            {
                output.Add((byte)(buffer >> (bitsLeft - 8)));
                bitsLeft -= 8;
            }
        }
        return output.ToArray();
    }
}
