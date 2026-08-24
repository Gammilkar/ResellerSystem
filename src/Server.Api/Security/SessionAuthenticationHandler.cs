using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ResellerSystem.Server.Application.Security;

namespace ResellerSystem.Server.Api.Security;

/// <summary>
/// Custom ASP.NET Core authentication scheme backed by the `sessions`
/// table (see ISessionService) — reads "Authorization: Bearer {token}",
/// validates it, and populates ClaimsPrincipal with the user id. Standard
/// ASP.NET Core extensibility point, no third-party auth package needed.
/// Controllers that require login use the ordinary [Authorize] attribute
/// against this scheme.
/// </summary>
public sealed class SessionAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string SchemeName = "Session";
}

public sealed class SessionAuthenticationHandler : AuthenticationHandler<SessionAuthenticationOptions>
{
    private readonly ISessionService _sessionService;

    public SessionAuthenticationHandler(
        IOptionsMonitor<SessionAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISessionService sessionService)
        : base(options, logger, encoder)
    {
        _sessionService = sessionService;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            return AuthenticateResult.NoResult();
        }

        var value = authHeader.ToString();
        if (!value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.NoResult();
        }

        var token = value["Bearer ".Length..].Trim();
        var userId = await _sessionService.ValidateTokenAsync(token, Context.RequestAborted);
        if (userId is null)
        {
            return AuthenticateResult.Fail("Invalid or expired session token.");
        }

        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()) };
        var identity = new ClaimsIdentity(claims, SessionAuthenticationOptions.SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SessionAuthenticationOptions.SchemeName);

        return AuthenticateResult.Success(ticket);
    }
}
