using System.Security.Cryptography;

namespace Lumina.Application.Auth;

/// <summary>
/// PBKDF2-SHA256 password hashing. Uses only built-in System.Security.Cryptography —
/// no external package — to keep Lumina.Application's dependency surface minimal.
/// Format: {iterations}.{base64(salt)}.{base64(hash)}
/// </summary>
public static class PasswordHasher
{
    private const int SaltSizeBytes = 16;
    private const int HashSizeBytes = 32;
    private const int Iterations = 210_000; // OWASP 2023+ recommendation for PBKDF2-SHA256

    public static string Hash(string plaintextPassword)
    {
        if (string.IsNullOrEmpty(plaintextPassword))
            throw new ArgumentException("Password cannot be empty.", nameof(plaintextPassword));

        var salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            plaintextPassword, salt, Iterations, HashAlgorithmName.SHA256, HashSizeBytes);

        return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string plaintextPassword, string storedHash)
    {
        var parts = storedHash.Split('.', 3);
        if (parts.Length != 3) return false;
        if (!int.TryParse(parts[0], out var iterations)) return false;

        byte[] salt, expectedHash;
        try
        {
            salt = Convert.FromBase64String(parts[1]);
            expectedHash = Convert.FromBase64String(parts[2]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actualHash = Rfc2898DeriveBytes.Pbkdf2(
            plaintextPassword, salt, iterations, HashAlgorithmName.SHA256, expectedHash.Length);

        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }
}
