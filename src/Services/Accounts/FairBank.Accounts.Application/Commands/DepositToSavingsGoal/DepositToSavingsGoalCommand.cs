using FairBank.Accounts.Domain.Enums;
using MediatR;

namespace FairBank.Accounts.Application.Commands.DepositToSavingsGoal;

public sealed record DepositToSavingsGoalCommand(
    Guid GoalId,
    decimal Amount,
    Currency Currency) : IRequest;
