using FairBank.Products.Application.Dtos;
using MediatR;

namespace FairBank.Products.Application.Queries.GetPendingApplications;

public sealed record GetPendingApplicationsQuery() : IRequest<IReadOnlyList<ProductApplicationResponse>>;
