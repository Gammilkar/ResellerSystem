namespace ResellerSystem.Web.Client.Services;

/// <summary>
/// Holds the bearer token and selected tenant database for one browser
/// connection ("circuit") — registered scoped in Server.Host's DI, so
/// Blazor Server gives each connected browser tab its own instance
/// automatically. Mirrors what ClientSessionState does for the desktop
/// app, just server-side per-circuit instead of in-process singleton.
/// </summary>
public sealed class CircuitState
{
    public string? Token { get; set; }
    public Guid? DatabaseId { get; set; }
    public string? DatabaseName { get; set; }

    public bool IsAuthenticated => !string.IsNullOrEmpty(Token);
    public bool HasDatabase => DatabaseId is not null;

    public void SignOut()
    {
        Token = null;
        DatabaseId = null;
        DatabaseName = null;
    }
}
