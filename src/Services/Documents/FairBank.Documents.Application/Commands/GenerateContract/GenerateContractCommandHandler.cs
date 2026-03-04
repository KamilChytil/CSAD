using FairBank.Documents.Application.DTOs;
using FairBank.Documents.Application.Ports;
using MediatR;

namespace FairBank.Documents.Application.Commands.GenerateContract;

public sealed class GenerateContractCommandHandler : IRequestHandler<GenerateContractCommand, ContractResponse>
{
    private readonly IProductsServiceClient _productsClient;
    private readonly IContractGenerator _generator;

    public GenerateContractCommandHandler(
        IProductsServiceClient productsClient,
        IContractGenerator generator)
    {
        _productsClient = productsClient;
        _generator = generator;
    }

    public async Task<ContractResponse> Handle(GenerateContractCommand request, CancellationToken ct)
    {
        var contract = await _productsClient.GetApplicationAsync(request.ApplicationId, ct);
        return await _generator.GenerateAsync(contract, request.Format);
    }
}
