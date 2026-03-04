using FairBank.Payments.Domain.Ports;
using FairBank.SharedKernel.Application;
using MediatR;

namespace FairBank.Payments.Application.Templates.Commands.DeactivateAllTemplatesByAccounts;

public sealed record DeactivateAllTemplatesByAccountsCommand(IReadOnlyList<Guid> AccountIds) : IRequest<int>;

public sealed class DeactivateAllTemplatesByAccountsCommandHandler(
    IPaymentTemplateRepository repository,
    IUnitOfWork unitOfWork) : IRequestHandler<DeactivateAllTemplatesByAccountsCommand, int>
{
    public async Task<int> Handle(DeactivateAllTemplatesByAccountsCommand request, CancellationToken ct)
    {
        var deleted = 0;

        foreach (var accountId in request.AccountIds)
        {
            var templates = await repository.GetByAccountIdAsync(accountId, ct);

            foreach (var template in templates)
            {
                if (!template.IsDeleted)
                {
                    template.SoftDelete();
                    await repository.UpdateAsync(template, ct);
                    deleted++;
                }
            }
        }

        if (deleted > 0)
            await unitOfWork.SaveChangesAsync(ct);

        return deleted;
    }
}
