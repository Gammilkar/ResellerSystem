using Microsoft.AspNetCore.Mvc;
using ResellerSystem.Domain.Shared.Dto;
using ResellerSystem.Server.Application.VersionInfo;

namespace ResellerSystem.Server.Api.Controllers;

[ApiController]
[Route("api/v1/version")]
public sealed class VersionController : ControllerBase
{
    private readonly IVersionProvider _versionProvider;

    public VersionController(IVersionProvider versionProvider)
    {
        _versionProvider = versionProvider;
    }

    [HttpGet]
    [ProducesResponseType(typeof(VersionResponse), StatusCodes.Status200OK)]
    public ActionResult<VersionResponse> Get() => Ok(_versionProvider.GetVersion());
}
