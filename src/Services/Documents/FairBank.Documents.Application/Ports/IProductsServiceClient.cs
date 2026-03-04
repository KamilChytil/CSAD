using FairBank.Documents.Application.DTOs;

namespace FairBank.Documents.Application.Ports;

public interface IProductsServiceClient
{
    Task<ProductContractDto> GetApplicationAsync(Guid applicationId, CancellationToken ct = default);
}
