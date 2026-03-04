using MediatR;

namespace FairBank.Accounts.Application.Commands.DeleteSavingsGoal;

public sealed record DeleteSavingsGoalCommand(Guid GoalId) : IRequest;
