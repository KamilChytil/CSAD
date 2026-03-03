using FairBank.Products.Application.Dtos;
using FairBank.Products.Domain.Repositories;
using FairBank.SharedKernel.Application;
using MediatR;

namespace FairBank.Products.Application.Commands.RejectApplication;

public sealed class RejectApplicationCommandHandler(
    IProductApplicationRepository repository,
    IUnitOfWork unitOfWork) : IRequestHandler<RejectApplicationCommand, ProductApplicationResponse>
{
    public async Task<ProductApplicationResponse> Handle(RejectApplicationCommand request, CancellationToken ct)
    {
        var application = await repository.GetByIdAsync(request.ApplicationId, ct)
            ?? throw new InvalidOperationException("Application not found.");

        application.Reject(request.ReviewerId, request.Note);
        await repository.UpdateAsync(application, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return new ProductApplicationResponse(
            application.Id, application.UserId, application.ProductType.ToString(),
            application.Status.ToString(), application.Parameters, application.MonthlyPayment,
            application.CreatedAt, application.ReviewedAt, application.ReviewedBy, application.Note);
    }
}
