using FairBank.Payments.Application.Services;
using MediatR;

namespace FairBank.Payments.Application.Commands.ParseQrPayment;

public sealed class ParseQrPaymentCommandHandler
    : IRequestHandler<ParseQrPaymentCommand, SpaydData?>
{
    public Task<SpaydData?> Handle(ParseQrPaymentCommand request, CancellationToken ct)
    {
        var result = SpaydParser.Parse(request.SpaydString);
        return Task.FromResult(result);
    }
}
