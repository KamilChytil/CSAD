using FairBank.Accounts.Application.DTOs;
using MediatR;

namespace FairBank.Accounts.Application.Queries.GetSavingsRulesByAccount;

public sealed record GetSavingsRulesByAccountQuery(Guid AccountId) : IRequest<IReadOnlyList<SavingsRuleResponse>>;
