using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResellerSystem.Domain.Shared.Dto;
using ResellerSystem.Server.Application.Backup;

namespace ResellerSystem.Server.Api.Controllers;

/// <summary>
/// Backup/Restore is gated behind [Authorize] — unlike Health/Version/
/// Databases, these are destructive/sensitive operations (Restore
/// overwrites live data) and must not be reachable by an unauthenticated
/// caller on the LAN.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/backups")]
public sealed class BackupsController : ControllerBase
{
    private readonly IBackupService _backupService;

    public BackupsController(IBackupService backupService)
    {
        _backupService = backupService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BackupManifestDto>>> List(CancellationToken ct)
    {
        var backups = await _backupService.ListBackupsAsync(ct);
        return Ok(backups.Select(ToDto).ToList());
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<BackupManifestDto>> Get(string id, CancellationToken ct)
    {
        var manifest = await _backupService.GetManifestAsync(id, ct);
        return Ok(ToDto(manifest));
    }

    [HttpPost]
    public async Task<ActionResult<BackupManifestDto>> Create([FromBody] CreateBackupRequest request, CancellationToken ct)
    {
        var manifest = request.Type == BackupTypeDto.Full
            ? await _backupService.CreateFullBackupAsync(ct)
            : await _backupService.CreateDatabaseBackupAsync(ct);

        return Ok(ToDto(manifest));
    }

    [HttpPost("{id}/restore")]
    public async Task<IActionResult> Restore(string id, CancellationToken ct)
    {
        await _backupService.RestoreAsync(id, ct);
        return NoContent();
    }

    private static BackupManifestDto ToDto(BackupManifest m) => new()
    {
        Id = m.Id,
        Type = m.Type == BackupType.Full ? BackupTypeDto.Full : BackupTypeDto.Database,
        CreatedAt = m.CreatedAt,
        ServerVersionAtBackup = m.ServerVersionAtBackup,
        Databases = m.Databases.Select(d => new BackupDatabaseEntryDto
        {
            PhysicalDatabaseName = d.PhysicalDatabaseName,
            IsMaster = d.IsMaster
        }).ToList(),
        TotalSizeBytes = m.TotalSizeBytes
    };
}
