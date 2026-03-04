using FairBank.Documents.Application.DTOs;
using FairBank.Documents.Application.Enums;
using MediatR;

namespace FairBank.Documents.Application.Commands.GenerateStatement;

public sealed record GenerateStatementCommand(
    Guid AccountId,
    DateTime? From,
    DateTime? To,
    StatementFormat Format) : IRequest<StatementResponse>;
