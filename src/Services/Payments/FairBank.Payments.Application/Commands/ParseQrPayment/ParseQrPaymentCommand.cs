using FairBank.Payments.Application.Services;
using MediatR;

namespace FairBank.Payments.Application.Commands.ParseQrPayment;

public sealed record ParseQrPaymentCommand(string SpaydString) : IRequest<SpaydData?>;
