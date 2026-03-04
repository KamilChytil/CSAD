using FairBank.Products.Application.Dtos;
using MediatR;

namespace FairBank.Products.Application.Commands.CancelApplication;

public sealed record CancelApplicationCommand(
    Guid ApplicationId,
    Guid UserId) : IRequest<ProductApplicationResponse>;
