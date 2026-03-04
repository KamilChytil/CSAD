using FairBank.Documents.Application.DTOs;
using FairBank.Documents.Application.Enums;
using MediatR;

namespace FairBank.Documents.Application.Commands.GenerateContract;

public sealed record GenerateContractCommand(
    Guid ApplicationId,
    ContractFormat Format) : IRequest<ContractResponse>;
