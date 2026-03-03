using System.Text;

namespace FairBank.Identity.Application.Helpers;

/// <summary>
/// Produces and parses the lightweight session tokens used by this service.
/// Format: Base64( "{userId}:{sessionId}" )
/// NOTE: This is NOT a signed JWT — it is only a safe-transport encoding.
///       Real authentication security comes from server-side ActiveSessionId matching.
/// </summary>
public static class SessionTokenHelper
{
    public static string Encode(Guid userId, Guid sessionId)
    {
        var raw = $"{userId}:{sessionId}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
    }

    public static bool TryDecode(string token, out Guid userId, out Guid sessionId)
    {
        userId = default;
        sessionId = default;

        try
        {
            var raw = Encoding.UTF8.GetString(Convert.FromBase64String(token));
            var parts = raw.Split(':');
            if (parts.Length != 2) return false;
            if (!Guid.TryParse(parts[0], out userId)) return false;
            if (!Guid.TryParse(parts[1], out sessionId)) return false;
            return true;
        }
        catch
        {
            return false;
        }
    }
}
