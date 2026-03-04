namespace FairBank.Documents.Application.DTOs;

public sealed record StatementResponse(
    byte[] Content,
    string ContentType,
    string FileName);
