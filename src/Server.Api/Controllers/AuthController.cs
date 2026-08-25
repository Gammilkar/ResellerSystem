using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResellerSystem.Domain.Shared.Dto;
using ResellerSystem.Server.Application.Security;

namespace ResellerSystem.Server.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthenticationService _authService;
    private readonly ISessionService _sessionService;

    public AuthController(IAuthenticationService authService, ISessionService sessionService)
    {
        _authService = authService;
        _sessionService = sessionService;
    }

    /// <summary>Lets Server Manager / Desktop clients know whether to show
    /// "create admin account" (first run) or a normal login screen.</summary>
    [HttpGet("status")]
    public async Task<ActionResult<AuthStatusResponse>> GetStatus(CancellationToken ct)
    {
        var needsSetup = await _authService.NeedsInitialSetupAsync(ct);
        return Ok(new AuthStatusResponse { NeedsInitialSetup = needsSetup });
    }

    /// <summary>Runs exactly once — creates the first (and, for now, only)
    /// local admin account. Refuses if any user already exists.</summary>
    [HttpPost("setup")]
    public async Task<IActionResult> InitialSetup([FromBody] InitialSetupRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { error = new { code = "VALIDATION_FAILED", message = "Username and password are required." } });
        }
        if (request.Password.Length < 8)
        {
            return BadRequest(new { error = new { code = "VALIDATION_FAILED", message = "Password must be at least 8 characters." } });
        }

        await _authService.CreateInitialAdminAsync(request.Username, request.Password, ct);
        return NoContent();
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var result = await _authService.LoginAsync(request.Username, request.Password, request.RememberMe, ct);
        if (!result.Success)
        {
            return Unauthorized(new { error = new { code = "INVALID_CREDENTIALS", message = result.FailureReason ?? "Login failed." } });
        }

        return Ok(new LoginResponse { Token = result.Token!, ExpiresAt = result.ExpiresAt!.Value });
    }

    /// <summary>Revokes the caller's own session token — used both for a
    /// normal sign-out and for "forget this device" (clears the client's
    /// persisted trusted-device token too, see TrustedDeviceStore).</summary>
    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var authHeader = Request.Headers.Authorization.ToString();
        if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            await _sessionService.RevokeAsync(authHeader["Bearer ".Length..].Trim(), ct);
        }
        return NoContent();
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        if (request.NewPassword.Length < 8)
        {
            return BadRequest(new { error = new { code = "VALIDATION_FAILED", message = "New password must be at least 8 characters." } });
        }

        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _authService.ChangePasswordAsync(userId, request.CurrentPassword, request.NewPassword, ct);
        if (!result.Success)
        {
            return BadRequest(new { error = new { code = "INVALID_CURRENT_PASSWORD", message = result.FailureReason ?? "Could not change password." } });
        }

        return NoContent();
    }
}
