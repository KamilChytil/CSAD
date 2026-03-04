using System.Security.Cryptography;

namespace FairBank.Identity.Application.Helpers;

public static class TotpHelper
{
    private const int SecretLength = 20;
    private const int CodeDigits = 6;
    private const int TimeStepSeconds = 30;
    private const int AllowedDrift = 1; // ±1 time step

    public static string GenerateSecret()
    {
        var bytes = RandomNumberGenerator.GetBytes(SecretLength);
        return Base32Encode(bytes);
    }

    public static bool VerifyCode(string secret, string code)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length != CodeDigits)
            return false;

        var secretBytes = Base32Decode(secret);
        var timeStep = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / TimeStepSeconds;

        for (var i = -AllowedDrift; i <= AllowedDrift; i++)
        {
            var expectedCode = GenerateCode(secretBytes, timeStep + i);
            if (expectedCode == code)
                return true;
        }

        return false;
    }

    public static string GetOtpAuthUri(string secret, string email, string issuer = "FairBank")
    {
        return $"otpauth://totp/{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(email)}" +
               $"?secret={secret}&issuer={Uri.EscapeDataString(issuer)}&digits={CodeDigits}&period={TimeStepSeconds}";
    }

    public static string[] GenerateBackupCodes(int count = 8)
    {
        var codes = new string[count];
        for (var i = 0; i < count; i++)
        {
            codes[i] = $"{RandomNumberGenerator.GetInt32(10000000, 99999999)}";
        }
        return codes;
    }

    private static string GenerateCode(byte[] secret, long timeStep)
    {
        var timeBytes = BitConverter.GetBytes(timeStep);
        if (BitConverter.IsLittleEndian) Array.Reverse(timeBytes);

        using var hmac = new HMACSHA1(secret);
        var hash = hmac.ComputeHash(timeBytes);

        var offset = hash[^1] & 0x0F;
        var binaryCode =
            ((hash[offset] & 0x7F) << 24) |
            ((hash[offset + 1] & 0xFF) << 16) |
            ((hash[offset + 2] & 0xFF) << 8) |
            (hash[offset + 3] & 0xFF);

        return (binaryCode % (int)Math.Pow(10, CodeDigits)).ToString().PadLeft(CodeDigits, '0');
    }

    private static string Base32Encode(byte[] data)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var result = new char[(data.Length * 8 + 4) / 5];
        var buffer = 0;
        var bitsLeft = 0;
        var index = 0;

        foreach (var b in data)
        {
            buffer = (buffer << 8) | b;
            bitsLeft += 8;
            while (bitsLeft >= 5)
            {
                result[index++] = alphabet[(buffer >> (bitsLeft - 5)) & 0x1F];
                bitsLeft -= 5;
            }
        }

        if (bitsLeft > 0)
            result[index] = alphabet[(buffer << (5 - bitsLeft)) & 0x1F];

        return new string(result);
    }

    private static byte[] Base32Decode(string encoded)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var output = new List<byte>();
        var buffer = 0;
        var bitsLeft = 0;

        foreach (var c in encoded.ToUpperInvariant())
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
