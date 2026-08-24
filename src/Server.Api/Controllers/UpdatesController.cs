using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResellerSystem.Domain.Shared.Dto;
using ResellerSystem.Server.Application.Update;

namespace ResellerSystem.Server.Api.Controllers;

/// <summary>
/// Check is public (Server Manager shows "update available" without
/// necessarily being logged in yet), Install requires authentication —
/// this is the single most disruptive action in the whole system (it
/// stops the service) and must not be triggerable by anyone on the LAN.
/// </summary>
[ApiController]
[Route("api/v1/updates")]
public sealed class UpdatesController : ControllerBase
{
    private readonly IUpdateService _updateService;

    public UpdatesController(IUpdateService updateService)
    {
        _updateService = updateService;
    }

    [HttpGet("check")]
    public async Task<ActionResult<UpdateCheckResultDto>> Check(CancellationToken ct)
    {
        var result = await _updateService.CheckForUpdateAsync(ct);
        return Ok(result);
    }

    [HttpPost("install")]
    [Authorize]
    public async Task<ActionResult<UpdateInstallResultDto>> Install(CancellationToken ct)
    {
        var result = await _updateService.BeginInstallAsync(ct);
        return Ok(result);
    }
}
