using System.Security.Cryptography;
using System.Text;

namespace LedgerNest.Infrastructure;

public static class PasswordCredentials
{
    // Strict version parsing also prevents imported records from requesting unbounded work.
    private const string Prefix = "pbkdf2-sha256$v1$600000$";
    public static string CreateSalt() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16));

    public static string Hash(string password, string salt) => Prefix + Convert.ToHexString(
        Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), Encoding.UTF8.GetBytes(salt),
            600_000, HashAlgorithmName.SHA256, 32));

    public static bool Verify(string password, string salt, string hash, out bool needsUpgrade)
    {
        needsUpgrade = false;
        if (string.IsNullOrEmpty(salt) || string.IsNullOrEmpty(hash)) return false;
        var modern = hash.StartsWith(Prefix, StringComparison.Ordinal);
        var encoded = modern ? hash[Prefix.Length..] : hash;
        if (encoded.Length != 64) return false;
        byte[] expected;
        try { expected = Convert.FromHexString(encoded); }
        catch (FormatException) { return false; }
        var actual = modern
            ? Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), Encoding.UTF8.GetBytes(salt), 600_000, HashAlgorithmName.SHA256, 32)
            : SHA256.HashData(Encoding.UTF8.GetBytes(salt + password));
        var valid = CryptographicOperations.FixedTimeEquals(actual, expected);
        needsUpgrade = valid && !modern;
        return valid;
    }
}
