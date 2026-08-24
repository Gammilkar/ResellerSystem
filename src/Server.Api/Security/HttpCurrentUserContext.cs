using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using ResellerSystem.Server.Domain.Abstractions;

namespace ResellerSystem.Server.Api.Security;

/// <summary>
/// Real ICurrentUserContext backed by the authenticated request's
/// ClaimsPrincipal (see SessionAuthenticationHandler) — replaces
/// SystemCurrentUserContext now that Security foundation exists. Falls
/// back to a "local-system" identity for requests with no session (Health/
/// Version/Databases stay usable without login, matching Architecture Plan
/// v0.1's "single small business, no heavy auth burden" principle) —
/// endpoints that actually need a real user (Backups, Updates) enforce
/// that via [Authorize], not via this class.
/// </summary>
public sealed class HttpCurrentUserContext : ICurrentUserContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpCurrentUserContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? Principal => _httpContextAccessor.HttpContext?.User;

    public string UserId
    {
        get
        {
            var idClaim = Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return idClaim ?? "local-system-user";
        }
    }

    public string DisplayName => Principal?.Identity?.IsAuthenticated == true ? UserId : "Local User";

    // No roles yet (Architecture Plan v0.1 — intentionally not building
    // multi-user permissions now); every authenticated user can access
    // every database, same as the Stage 1 stub.
    public bool CanAccessAllDatabases => true;
    public IReadOnlyCollection<Guid> AllowedDatabaseIds => Array.Empty<Guid>();
}
