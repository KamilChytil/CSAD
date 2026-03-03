using FairBank.Products.Application.Dtos;
using MediatR;

namespace FairBank.Products.Application.Queries.GetUserApplications;

public sealed record GetUserApplicationsQuery(Guid UserId) : IRequest<IReadOnlyList<ProductApplicationResponse>>;
