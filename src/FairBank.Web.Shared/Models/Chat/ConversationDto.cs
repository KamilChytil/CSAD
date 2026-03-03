namespace FairBank.Web.Shared.Models.Chat;

public sealed record ConversationDto(
    Guid Id,
    string Type,   // "Support" | "Family"
    string Label,
    string? LastMessage,
    DateTime? LastMessageAt
);
