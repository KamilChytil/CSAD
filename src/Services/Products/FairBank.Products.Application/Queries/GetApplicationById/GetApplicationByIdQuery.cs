using FairBank.Products.Application.Dtos;
using MediatR;

namespace FairBank.Products.Application.Queries.GetApplicationById;

public sealed record GetApplicationByIdQuery(Guid ApplicationId) : IRequest<ProductApplicationResponse?>;
