namespace FairBank.Payments.Application.DTOs;

public sealed record StandingOrderResponse(
    Guid Id,
    Guid SenderAccountId,
    string SenderAccountNumber,
    string RecipientAccountNumber,
    decimal Amount,
    string Currency,
    string? Description,
    string Interval,
    DateTime NextExecutionDate,
    DateTime? EndDate,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? LastExecutedAt,
    int ExecutionCount);
