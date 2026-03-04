namespace FairBank.Cards.Application.DTOs;

public sealed record CardResponse(
    Guid Id,
    Guid AccountId,
    Guid UserId,
    string MaskedCardNumber,
    string LastFourDigits,
    string CardholderName,
    DateOnly ExpirationDate,
    string CardType,
    string CardBrand,
    string Status,
    decimal DailyLimit,
    decimal MonthlyLimit,
    bool OnlinePaymentsEnabled,
    bool ContactlessEnabled,
    bool HasPin,
    DateTime CreatedAt);
