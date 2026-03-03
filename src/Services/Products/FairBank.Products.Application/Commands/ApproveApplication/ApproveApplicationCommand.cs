using FairBank.Products.Application.Dtos;
using MediatR;

namespace FairBank.Products.Application.Commands.ApproveApplication;

public sealed record ApproveApplicationCommand(
    Guid ApplicationId,
    Guid ReviewerId,
    string? Note = null) : IRequest<ProductApplicationResponse>;
