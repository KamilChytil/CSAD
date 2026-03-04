using FairBank.Accounts.Application.DTOs;
using FairBank.Accounts.Domain.Enums;
using MediatR;

namespace FairBank.Accounts.Application.Commands.CreateSavingsRule;

public sealed record CreateSavingsRuleCommand(
    Guid AccountId,
    string Name,
    string? Description,
    SavingsRuleType Type,
    decimal Amount) : IRequest<SavingsRuleResponse>;
