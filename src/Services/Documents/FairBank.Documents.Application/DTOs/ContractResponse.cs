namespace FairBank.Documents.Application.DTOs;

public sealed record ContractResponse(
    byte[] Content,
    string ContentType,
    string FileName);
