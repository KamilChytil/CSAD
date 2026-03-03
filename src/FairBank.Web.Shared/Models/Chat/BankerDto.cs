namespace FairBank.Web.Shared.Models.Chat;

public sealed record BankerDto(
    Guid Id,
    string FirstName,
    string LastName,
    string Email);
