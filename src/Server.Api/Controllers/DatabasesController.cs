using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResellerSystem.Domain.Shared.Dto;
using ResellerSystem.Server.Application.Databases;

namespace ResellerSystem.Server.Api.Controllers;

/// <summary>
/// Manages tenant database *profiles* (the master-database registry).
/// Never exposes PhysicalDatabaseName, connection strings, or credentials —
/// see DatabaseProfileMapper. Deletion is intentionally not exposed here yet
/// (architecture requires a dedicated safe-delete procedure, not a plain
/// DELETE verb — see Architecture Plan section 7).
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/databases")]
public sealed class DatabasesController : ControllerBase
{
    private readonly IDatabaseProvisioningService _provisioningService;

    public DatabasesController(IDatabaseProvisioningService provisioningService)
    {
        _provisioningService = provisioningService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<DatabaseProfileDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DatabaseProfileDto>>> List(CancellationToken ct)
    {
        var databases = await _provisioningService.ListAsync(ct);
        return Ok(databases);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(DatabaseProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DatabaseProfileDto>> GetById(Guid id, CancellationToken ct)
    {
        var database = await _provisioningService.GetAsync(id, ct);
        return Ok(database);
    }

    [HttpPost]
    [ProducesResponseType(typeof(DatabaseProfileDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DatabaseProfileDto>> Create([FromBody] CreateDatabaseRequest request, CancellationToken ct)
    {
        var created = await _provisioningService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPatch("{id:guid}")]
    [ProducesResponseType(typeof(DatabaseProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DatabaseProfileDto>> Update(Guid id, [FromBody] UpdateDatabaseRequest request, CancellationToken ct)
    {
        var updated = await _provisioningService.UpdateAsync(id, request, ct);
        return Ok(updated);
    }
}
