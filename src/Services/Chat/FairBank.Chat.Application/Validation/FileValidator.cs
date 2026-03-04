namespace FairBank.Chat.Application.Validation;

public static class FileValidator
{
    public const long MaxFileSize = 10 * 1024 * 1024; // 10 MB

    public static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/gif", "image/webp",
        "application/pdf",
        "text/plain",
        "application/msword",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
    };

    public static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".webp", ".pdf", ".txt", ".doc", ".docx"
    };

    public static bool IsContentTypeAllowed(string contentType)
        => AllowedContentTypes.Contains(contentType);

    public static bool IsExtensionAllowed(string? extension)
        => !string.IsNullOrEmpty(extension) && AllowedExtensions.Contains(extension);

    public static bool IsFileSizeValid(long fileSize)
        => fileSize > 0 && fileSize <= MaxFileSize;

    public static bool ValidateMagicBytes(byte[] fileBytes, string contentType)
    {
        if (fileBytes.Length < 4)
            return false;

        return contentType.ToLowerInvariant() switch
        {
            "image/jpeg" => fileBytes.Length >= 3
                            && fileBytes[0] == 0xFF
                            && fileBytes[1] == 0xD8
                            && fileBytes[2] == 0xFF,

            "image/png" => fileBytes.Length >= 4
                           && fileBytes[0] == 0x89
                           && fileBytes[1] == 0x50
                           && fileBytes[2] == 0x4E
                           && fileBytes[3] == 0x47,

            "image/gif" => fileBytes.Length >= 4
                           && fileBytes[0] == 0x47
                           && fileBytes[1] == 0x49
                           && fileBytes[2] == 0x46
                           && fileBytes[3] == 0x38,

            "application/pdf" => fileBytes.Length >= 4
                                 && fileBytes[0] == 0x25
                                 && fileBytes[1] == 0x50
                                 && fileBytes[2] == 0x44
                                 && fileBytes[3] == 0x46,

            _ => true
        };
    }
}
