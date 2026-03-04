namespace FairBank.Documents.Application.DTOs;

public sealed record ProductContractDto(
    Guid ApplicationId,
    Guid UserId,
    string ProductType,
    string Status,
    string Parameters,
    decimal MonthlyPayment,
    DateTime CreatedAt,
    string UserName,
    string UserEmail);
