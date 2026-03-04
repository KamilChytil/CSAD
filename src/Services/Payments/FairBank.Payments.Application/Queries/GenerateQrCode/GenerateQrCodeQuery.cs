using MediatR;

namespace FairBank.Payments.Application.Queries.GenerateQrCode;

public sealed record GenerateQrCodeQuery(
    string AccountNumber,
    decimal? Amount = null,
    string Currency = "CZK",
    string? Message = null) : IRequest<QrCodeResult>;

public sealed record QrCodeResult(string Base64Image, string SpaydString);
