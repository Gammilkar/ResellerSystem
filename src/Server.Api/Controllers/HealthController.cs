using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using ResellerSystem.Domain.Shared.Dto;
using ResellerSystem.Server.Application.VersionInfo;
using ResellerSystem.Server.Data.Master;
using ResellerSystem.Server.FileStorage;

namespace ResellerSystem.Server.Api.Controllers;

[ApiController]
[Route("health")]
public sealed class HealthController : ControllerBase
{
    private readonly IMasterDatabaseHealthChecker _masterDbHealth;
    private readonly IFileStorageService _fileStorage;
    private readonly IVersionProvider _versionProvider;
    private readonly IHostEnvironment _environment;

    public HealthController(
        IMasterDatabaseHealthChecker masterDbHealth,
        IFileStorageService fileStorage,
        IVersionProvider versionProvider,
        IHostEnvironment environment)
    {
        _masterDbHealth = masterDbHealth;
        _fileStorage = fileStorage;
        _versionProvider = versionProvider;
        _environment = environment;
    }

    [HttpGet]
    [ProducesResponseType(typeof(HealthResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<HealthResponse>> Get(CancellationToken ct)
    {
        var masterHealthy = await _masterDbHealth.IsHealthyAsync(ct);
        var storageHealthy = await _fileStorage.CheckReadWriteAsync(ct);

        var overallStatus = masterHealthy && storageHealthy ? "healthy"
            : masterHealthy || storageHealthy ? "degraded"
            : "unhealthy";

        var response = new HealthResponse
        {
            Status = overallStatus,
            ServerVersion = _versionProvider.ServerVersion,
            MasterDatabase = masterHealthy ? "healthy" : "unhealthy",
            FileStorage = storageHealthy ? "healthy" : "unhealthy",
            AvailableDiskSpaceBytes = _fileStorage.GetAvailableDiskSpaceBytes(),
            TimeUtc = DateTimeOffset.UtcNow,
            Environment = _environment.EnvironmentName
        };

        // Never include passwords, connection strings, or file paths here —
        // only the booleans/numbers above.
        return Ok(response);
    }
}
