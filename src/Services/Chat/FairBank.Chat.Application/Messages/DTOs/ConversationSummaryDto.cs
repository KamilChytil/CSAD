namespace FairBank.Chat.Application.Messages.DTOs;

public sealed record ConversationSummaryDto(
    Guid Id,
    string Type,   // "Support" | "Family"
    string Label,
    string? LastMessage,
    DateTime? LastMessageAt,
    string Status,
    DateTime? ClosedAt,
    Guid? AssignedBankerId,
    string? InternalNotes
);
