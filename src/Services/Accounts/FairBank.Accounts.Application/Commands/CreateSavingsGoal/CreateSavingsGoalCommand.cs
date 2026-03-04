using FairBank.Accounts.Application.DTOs;
using FairBank.Accounts.Domain.Enums;
using MediatR;

namespace FairBank.Accounts.Application.Commands.CreateSavingsGoal;

public sealed record CreateSavingsGoalCommand(
    Guid AccountId,
    string Name,
    string? Description,
    decimal TargetAmount,
    Currency Currency) : IRequest<SavingsGoalResponse>;
