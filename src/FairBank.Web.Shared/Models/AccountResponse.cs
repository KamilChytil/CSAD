namespace FairBank.Web.Shared.Models;

public sealed record AccountResponse(
    Guid Id,
    Guid OwnerId,
    string AccountNumber,
    decimal Balance,
    string Currency,
    bool IsActive,
    DateTime CreatedAt,
    bool RequiresApproval = false,
    decimal? ApprovalThreshold = null,
    decimal? SpendingLimit = null,
    string AccountType = "Checking")
{
    /// <summary>Czech display label for the account type.</summary>
    public string TypeLabel => AccountType == "Savings" ? "Spořicí účet" : "Běžný účet";
}
