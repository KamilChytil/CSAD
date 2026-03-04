using FairBank.Products.Application.Dtos;
using FairBank.Products.Domain.Repositories;
using MediatR;

namespace FairBank.Products.Application.Queries.GetUserApplications;

public sealed class GetUserApplicationsQueryHandler(
    IProductApplicationRepository repository) : IRequestHandler<GetUserApplicationsQuery, IReadOnlyList<ProductApplicationResponse>>
{
    public async Task<IReadOnlyList<ProductApplicationResponse>> Handle(GetUserApplicationsQuery request, CancellationToken ct)
    {
        var applications = await repository.GetByUserIdAsync(request.UserId, ct);
        return applications.Select(a => new ProductApplicationResponse(
            a.Id, a.UserId, a.ProductType.ToString(), a.Status.ToString(),
            a.Parameters, a.MonthlyPayment, a.CreatedAt,
            a.ReviewedAt, a.ReviewedBy, a.Note)).ToList();
    }
}
