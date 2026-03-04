namespace FairBank.Web.Shared.Models;

public sealed record BankerClientDto(
    Guid ClientId,
    string ClientLabel,
    int ActiveChatsCount,
    DateTime? LastActivity);
