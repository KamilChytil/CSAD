using FairBank.Products.Application.Dtos;
using MediatR;

namespace FairBank.Products.Application.Commands.SubmitApplication;

public sealed record SubmitApplicationCommand(
    Guid UserId,
    string ProductType,
    string Parameters,
    decimal MonthlyPayment) : IRequest<ProductApplicationResponse>;
