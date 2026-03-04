using FairBank.Payments.Application.Services;
using MediatR;
using QRCoder;

namespace FairBank.Payments.Application.Queries.GenerateQrCode;

public sealed class GenerateQrCodeQueryHandler
    : IRequestHandler<GenerateQrCodeQuery, QrCodeResult>
{
    public Task<QrCodeResult> Handle(GenerateQrCodeQuery request, CancellationToken ct)
    {
        var spayd = SpaydGenerator.Generate(
            request.AccountNumber,
            request.Amount,
            request.Currency,
            request.Message);

        using var qrGenerator = new QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(spayd, QRCodeGenerator.ECCLevel.M);
        using var pngQrCode = new PngByteQRCode(qrCodeData);
        var pngBytes = pngQrCode.GetGraphic(10);

        var base64 = Convert.ToBase64String(pngBytes);

        return Task.FromResult(new QrCodeResult($"data:image/png;base64,{base64}", spayd));
    }
}
