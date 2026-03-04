using FairBank.Accounts.Domain.Enums;
using MediatR;

namespace FairBank.Accounts.Application.Commands.WithdrawFromSavingsGoal;

public sealed record WithdrawFromSavingsGoalCommand(
    Guid GoalId,
    decimal Amount,
    Currency Currency) : IRequest;
