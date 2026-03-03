using FairBank.Products.Application.Dtos;
using MediatR;

namespace FairBank.Products.Application.Commands.RejectApplication;

public sealed record RejectApplicationCommand(
    Guid ApplicationId,
    Guid ReviewerId,
    string? Note = null) : IRequest<ProductApplicationResponse>;
