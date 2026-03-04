using System.Security.Cryptography;
using Konscious.Security.Cryptography;

namespace FairBank.SharedKernel.Security;

/// <summary>
/// Argon2id password hasher. Format: $argon2id$m=65536,t=3,p=1$&lt;salt-b64&gt;$&lt;hash-b64&gt;
/// </summary>
public static class PasswordHasher
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int DegreeOfParallelism = 1;
    private const int MemorySize = 65536; // 64 MB
    private const int Iterations = 3;

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = ComputeHash(password, salt);

        var saltB64 = Convert.ToBase64String(salt);
        var hashB64 = Convert.ToBase64String(hash);

        return $"$argon2id$m={MemorySize},t={Iterations},p={DegreeOfParallelism}${saltB64}${hashB64}";
    }

    public static bool Verify(string password, string encodedHash)
    {
        // Support BCrypt hashes for backward compatibility during migration
        if (encodedHash.StartsWith("$2"))
            return BCrypt.Net.BCrypt.Verify(password, encodedHash);

        var parts = encodedHash.Split('$', StringSplitOptions.RemoveEmptyEntries);
        // Expected: argon2id | m=...,t=...,p=... | salt | hash
        if (parts.Length != 4 || parts[0] != "argon2id")
            return false;

        var salt = Convert.FromBase64String(parts[2]);
        var expectedHash = Convert.FromBase64String(parts[3]);

        var computedHash = ComputeHash(password, salt);

        return CryptographicOperations.FixedTimeEquals(computedHash, expectedHash);
    }

    private static byte[] ComputeHash(string password, byte[] salt)
    {
        using var argon2 = new Argon2id(System.Text.Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            DegreeOfParallelism = DegreeOfParallelism,
            MemorySize = MemorySize,
            Iterations = Iterations
        };

        return argon2.GetBytes(HashSize);
    }
}
