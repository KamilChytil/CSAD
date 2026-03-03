using FairBank.Web.Shared.Models;

namespace FairBank.Web.Shared.Services;

public interface IAuthService
{
    /// <summary>Aktuální přihlášená relace (null = nepřihlášen).</summary>
    AuthSession? CurrentSession { get; }

    bool IsAuthenticated { get; }

    /// <summary>Zbývající neúspěšné pokusy před zámkem.</summary>
    int RemainingAttempts { get; }

    /// <summary>Čas, kdy bude zámek odemčen (null = nezamčeno).</summary>
    DateTime? LockedUntil { get; }

    bool IsLocked { get; }

    /// <summary>True pokud relace existovala, ale vypršela nebo byla zneplatněna. False při prvním přístupu bez relace.</summary>
    bool WasSessionExpired { get; }

    event Action? AuthStateChanged;

    Task<LoginResponse?> LoginAsync(LoginRequest request);
    Task LogoutAsync();
    Task<UserResponse?> RegisterAsync(RegisterRequest request);

    /// <summary>Obnoví timeout při aktivitě uživatele.</summary>
    void ResetInactivityTimer();

    /// <summary>Načte relaci z localStorage při startu aplikace.</summary>
    Task InitializeAsync();

    /// <summary>Ověří, zda je relace stále platná na serveru.</summary>
    Task<bool> ValidateSessionAsync();
}
