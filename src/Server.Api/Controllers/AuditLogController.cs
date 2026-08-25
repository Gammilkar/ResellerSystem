using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResellerSystem.Domain.Shared.Dto;
using ResellerSystem.Server.Application.Audit;

namespace ResellerSystem.Server.Api.Controllers;

/// <summary>Product Specification section 78 ("Audit Log"). Read-only —
/// entries are written by the services that make the change (see
/// IAuditLogger call sites), never posted here directly.</summary>
[ApiController]
[Authorize]
[Route("api/v1/audit-log")]
public sealed class AuditLogController : ControllerBase
{
    private readonly IAuditLogger _auditLogger;
    public AuditLogController(IAuditLogger auditLogger) => _auditLogger = auditLogger;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AuditLogEntryDto>>> List(
        [FromQuery] string? entityType, [FromQuery] Guid? entityId, [FromQuery] int limit, CancellationToken ct)
    {
        var entries = await _auditLogger.ListAsync(entityType, entityId, limit <= 0 ? 200 : Math.Min(limit, 1000), ct);
        return Ok(entries.Select(e => new AuditLogEntryDto
        {
            Id = e.Id,
            EntityType = e.EntityType,
            EntityId = e.EntityId,
            Action = e.Action,
            FieldName = e.FieldName,
            OldValue = e.OldValue,
            NewValue = e.NewValue,
            ChangedAt = e.ChangedAt,
            ChangedBy = e.ChangedBy,
            Source = e.Source
        }).ToList());
    }
}
