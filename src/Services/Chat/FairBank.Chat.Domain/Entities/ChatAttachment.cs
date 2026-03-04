namespace FairBank.Chat.Domain.Entities;

public sealed class ChatAttachment
{
    public Guid Id { get; private set; }
    public Guid MessageId { get; private set; }
    public string FileName { get; private set; } = null!;
    public string ContentType { get; private set; } = null!;
    public long FileSize { get; private set; }
    public string StoragePath { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }

    private ChatAttachment() { }

    public static ChatAttachment Create(Guid messageId, string fileName, string contentType, long fileSize, string storagePath)
    {
        if (fileSize > 10 * 1024 * 1024) // 10 MB
            throw new InvalidOperationException("File size exceeds maximum of 10 MB.");

        var allowedTypes = new[] { "application/pdf", "image/png", "image/jpeg", "image/jpg",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document" };

        if (!allowedTypes.Contains(contentType.ToLowerInvariant()))
            throw new InvalidOperationException($"File type '{contentType}' is not allowed.");

        return new ChatAttachment
        {
            Id = Guid.NewGuid(),
            MessageId = messageId,
            FileName = fileName,
            ContentType = contentType,
            FileSize = fileSize,
            StoragePath = storagePath,
            CreatedAt = DateTime.UtcNow
        };
    }
}
