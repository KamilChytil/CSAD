using FairBank.Products.Application.Dtos;
using FairBank.Products.Domain.Repositories;
using MediatR;

namespace FairBank.Products.Application.Queries.GetApplicationById;

public sealed class GetApplicationByIdQueryHandler(
    IProductApplicationRepository repository) : IRequestHandler<GetApplicationByIdQuery, ProductApplicationResponse?>
{
    public async Task<ProductApplicationResponse?> Handle(GetApplicationByIdQuery request, CancellationToken ct)
    {
        var a = await repository.GetByIdAsync(request.ApplicationId, ct);
        if (a is null) return null;

        return new ProductApplicationResponse(
            a.Id, a.UserId, a.ProductType.ToString(), a.Status.ToString(),
            a.Parameters, a.MonthlyPayment, a.CreatedAt,
            a.ReviewedAt, a.ReviewedBy, a.Note);
    }
}
