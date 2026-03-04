using FairBank.Documents.Application.DTOs;
using FairBank.Documents.Application.Enums;

namespace FairBank.Documents.Application.Ports;

public interface IContractGenerator
{
    Task<ContractResponse> GenerateAsync(
        ProductContractDto contract,
        ContractFormat format);
}
