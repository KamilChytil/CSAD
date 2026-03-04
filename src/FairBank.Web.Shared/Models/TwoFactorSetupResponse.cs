namespace FairBank.Web.Shared.Models;

public class TwoFactorSetupResponse
{
    public string Secret { get; set; } = "";
    public string OtpAuthUri { get; set; } = "";
    public bool IsAlreadyEnabled { get; set; }
}
