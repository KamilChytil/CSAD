namespace FairBank.Web.Shared.Models;

public sealed record RegisterRequest(
    string FirstName,
    string LastName,
    string Email,
    string Password,
    string PasswordConfirm,
    string Phone,
    DateTime DateOfBirth,
    string PersonalIdNumber,
    string Street,
    string City,
    string ZipCode,
    string Country);
