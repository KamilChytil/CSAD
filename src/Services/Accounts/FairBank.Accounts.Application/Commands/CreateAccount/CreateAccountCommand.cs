using FairBank.Accounts.Application.DTOs;
using FairBank.Accounts.Domain.Enums;
using MediatR;

namespace FairBank.Accounts.Application.Commands.CreateAccount;

public sealed record CreateAccountCommand(
    Guid OwnerId,
    Currency Currency = Currency.CZK) : IRequest<AccountResponse>;
