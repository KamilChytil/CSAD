using FairBank.Accounts.Application.DTOs;
using MediatR;

namespace FairBank.Accounts.Application.Queries.GetSavingsGoalsByAccount;

public sealed record GetSavingsGoalsByAccountQuery(Guid AccountId) : IRequest<IReadOnlyList<SavingsGoalResponse>>;
