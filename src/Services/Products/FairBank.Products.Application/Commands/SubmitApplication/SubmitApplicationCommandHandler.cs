using FairBank.Products.Application.Dtos;
using FairBank.Products.Domain.Enums;
using FairBank.Products.Domain.Repositories;
using FairBank.SharedKernel.Application;
using MediatR;

namespace FairBank.Products.Application.Commands.SubmitApplication;

public sealed class SubmitApplicationCommandHandler(
    IProductApplicationRepository repository,
    IUnitOfWork unitOfWork) : IRequestHandler<SubmitApplicationCommand, ProductApplicationResponse>
{
    public async Task<ProductApplicationResponse> Handle(SubmitApplicationCommand request, CancellationToken ct)
    {
        if (!Enum.TryParse<ProductType>(request.ProductType, true, out var productType))
            throw new ArgumentException($"Invalid product type: {request.ProductType}");

        var application = Domain.Entities.ProductApplication.Create(
            request.UserId,
            productType,
            request.Parameters,
            request.MonthlyPayment);

        await repository.AddAsync(application, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return MapToResponse(application);
    }

    private static ProductApplicationResponse MapToResponse(Domain.Entities.ProductApplication a) => new(
        a.Id, a.UserId, a.ProductType.ToString(), a.Status.ToString(),
        a.Parameters, a.MonthlyPayment, a.CreatedAt,
        a.ReviewedAt, a.ReviewedBy, a.Note);
}
