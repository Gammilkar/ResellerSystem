using Microsoft.AspNetCore.Mvc;
using ResellerSystem.Domain.Shared.Dto;
using ResellerSystem.Server.Application.Security;

namespace ResellerSystem.Server.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthenticationService _authService;

    public AuthController(IAuthenticationService authService)
    {
        _authService = authService;
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
        var result = await _authService.LoginAsync(request.Username, request.Password, ct);
        if (!result.Success)
        {
            return Unauthorized(new { error = new { code = "INVALID_CREDENTIALS", message = result.FailureReason ?? "Login failed." } });
        }

        return Ok(new LoginResponse { Token = result.Token!, ExpiresAt = result.ExpiresAt!.Value });
    }
}
