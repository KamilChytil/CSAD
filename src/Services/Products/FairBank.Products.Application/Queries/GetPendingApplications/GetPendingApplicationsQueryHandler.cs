using FairBank.Products.Application.Dtos;
using FairBank.Products.Domain.Repositories;
using MediatR;

namespace FairBank.Products.Application.Queries.GetPendingApplications;

public sealed class GetPendingApplicationsQueryHandler(
    IProductApplicationRepository repository) : IRequestHandler<GetPendingApplicationsQuery, IReadOnlyList<ProductApplicationResponse>>
{
    public async Task<IReadOnlyList<ProductApplicationResponse>> Handle(GetPendingApplicationsQuery request, CancellationToken ct)
    {
        var applications = await repository.GetPendingAsync(ct);
        return applications.Select(a => new ProductApplicationResponse(
            a.Id, a.UserId, a.ProductType.ToString(), a.Status.ToString(),
            a.Parameters, a.MonthlyPayment, a.CreatedAt,
            a.ReviewedAt, a.ReviewedBy, a.Note)).ToList();
    }
}
