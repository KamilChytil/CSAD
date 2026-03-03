using FairBank.Payments.Domain.Ports;
using FairBank.SharedKernel.Application;
using MediatR;

namespace FairBank.Payments.Application.Templates.Commands.DeleteTemplate;

public sealed record DeleteTemplateCommand(Guid TemplateId) : IRequest<bool>;

public sealed class DeleteTemplateCommandHandler(
    IPaymentTemplateRepository repository,
    IUnitOfWork unitOfWork) : IRequestHandler<DeleteTemplateCommand, bool>
{
    public async Task<bool> Handle(DeleteTemplateCommand request, CancellationToken ct)
    {
        var template = await repository.GetByIdAsync(request.TemplateId, ct)
            ?? throw new InvalidOperationException("Template not found.");

        template.SoftDelete();
        await repository.UpdateAsync(template, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return true;
    }
}
