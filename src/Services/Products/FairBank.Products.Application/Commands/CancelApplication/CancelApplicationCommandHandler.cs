using FairBank.Products.Application.Dtos;
using FairBank.Products.Domain.Repositories;
using FairBank.SharedKernel.Application;
using MediatR;

namespace FairBank.Products.Application.Commands.CancelApplication;

public sealed class CancelApplicationCommandHandler(
    IProductApplicationRepository repository,
    IUnitOfWork unitOfWork) : IRequestHandler<CancelApplicationCommand, ProductApplicationResponse>
{
    public async Task<ProductApplicationResponse> Handle(CancelApplicationCommand request, CancellationToken ct)
    {
        var application = await repository.GetByIdAsync(request.ApplicationId, ct)
            ?? throw new InvalidOperationException("Application not found.");

        if (application.UserId != request.UserId)
            throw new InvalidOperationException("Only the applicant can cancel their application.");

        application.Cancel();
        await repository.UpdateAsync(application, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return new ProductApplicationResponse(
            application.Id, application.UserId, application.ProductType.ToString(),
            application.Status.ToString(), application.Parameters, application.MonthlyPayment,
            application.CreatedAt, application.ReviewedAt, application.ReviewedBy, application.Note);
    }
}
