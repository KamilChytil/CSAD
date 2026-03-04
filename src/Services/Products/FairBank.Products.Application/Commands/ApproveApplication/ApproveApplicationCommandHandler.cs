using FairBank.Products.Application.Dtos;
using FairBank.Products.Domain.Repositories;
using FairBank.SharedKernel.Application;
using MediatR;

namespace FairBank.Products.Application.Commands.ApproveApplication;

public sealed class ApproveApplicationCommandHandler(
    IProductApplicationRepository repository,
    IUnitOfWork unitOfWork) : IRequestHandler<ApproveApplicationCommand, ProductApplicationResponse>
{
    public async Task<ProductApplicationResponse> Handle(ApproveApplicationCommand request, CancellationToken ct)
    {
        var application = await repository.GetByIdAsync(request.ApplicationId, ct)
            ?? throw new InvalidOperationException("Application not found.");

        application.Approve(request.ReviewerId, request.Note);
        await repository.UpdateAsync(application, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return new ProductApplicationResponse(
            application.Id, application.UserId, application.ProductType.ToString(),
            application.Status.ToString(), application.Parameters, application.MonthlyPayment,
            application.CreatedAt, application.ReviewedAt, application.ReviewedBy, application.Note);
    }
}
