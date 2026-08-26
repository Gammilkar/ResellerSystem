using Microsoft.EntityFrameworkCore;
using ResellerSystem.Domain.Shared.Dto;
using ResellerSystem.Modules.Inventory.Data;
using ResellerSystem.Modules.Inventory.Domain;
using ResellerSystem.Server.Application.Audit;
using ResellerSystem.Server.Application.Exceptions;
using ResellerSystem.Server.Domain.Abstractions;

namespace ResellerSystem.Modules.Inventory.Application;

/// <summary>The generic "справочник-конструктор" mechanism (Product
/// Specification §76) — one service, keyed by ListKey, backs every
/// user-editable picklist this module needs. See ReferenceListValue's
/// doc-comment for why this is one table rather than one per concept.</summary>
public interface IReferenceListService
{
    Task<IReadOnlyList<ReferenceListValueDto>> ListAsync(string listKey, CancellationToken ct = default);
    Task<ReferenceListValueDto> CreateAsync(CreateReferenceListValueRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public sealed class ReferenceListService : IReferenceListService
{
    private readonly IInventoryDbContextFactory _dbContextFactory;
    private readonly IAuditLogger _auditLogger;
    private readonly ICurrentUserContext _currentUser;

    public ReferenceListService(IInventoryDbContextFactory dbContextFactory, IAuditLogger auditLogger, ICurrentUserContext currentUser)
    {
        _dbContextFactory = dbContextFactory;
        _auditLogger = auditLogger;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<ReferenceListValueDto>> ListAsync(string listKey, CancellationToken ct = default)
    {
        await using var db = _dbContextFactory.CreateForCurrentTenant();
        return await db.ReferenceListValues
            .Where(v => v.ListKey == listKey)
            .OrderBy(v => v.SortOrder).ThenBy(v => v.Value)
            .Select(v => ToDto(v))
            .ToListAsync(ct);
    }

    public async Task<ReferenceListValueDto> CreateAsync(CreateReferenceListValueRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.ListKey))
            throw new ValidationFailedException(new[] { "List key is required." });
        if (string.IsNullOrWhiteSpace(request.Value))
            throw new ValidationFailedException(new[] { "Value is required." });

        await using var db = _dbContextFactory.CreateForCurrentTenant();

        var trimmed = request.Value.Trim();
        var exists = await db.ReferenceListValues.AnyAsync(v => v.ListKey == request.ListKey && v.Value == trimmed, ct);
        if (exists) throw new ValidationFailedException(new[] { "This value already exists in the list." });

        var value = ReferenceListValue.CreateNew(request.ListKey, request.Value, request.SortOrder);
        value.CreatedBy = _currentUser.DisplayName;
        value.UpdatedBy = _currentUser.DisplayName;
        db.ReferenceListValues.Add(value);
        await db.SaveChangesAsync(ct);

        await _auditLogger.LogAsync(new AuditEntry("ReferenceListValue", value.Id, "Created", _currentUser.DisplayName, "manual"), ct);
        return ToDto(value);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = _dbContextFactory.CreateForCurrentTenant();

        var value = await db.ReferenceListValues.FirstOrDefaultAsync(v => v.Id == id, ct)
            ?? throw new NotFoundException("REFERENCE_LIST_VALUE_NOT_FOUND", "Value was not found.");

        // Deletable even if system-default: existing Purchases/lines keep
        // their string value regardless (denormalized, no FK), only future
        // pick-lists stop offering it.
        value.SoftDelete();
        await db.SaveChangesAsync(ct);

        await _auditLogger.LogAsync(new AuditEntry("ReferenceListValue", value.Id, "Deleted", _currentUser.DisplayName, "manual"), ct);
    }

    private static ReferenceListValueDto ToDto(ReferenceListValue v) => new()
    {
        Id = v.Id,
        ListKey = v.ListKey,
        Value = v.Value,
        SortOrder = v.SortOrder,
        IsSystemDefault = v.IsSystemDefault
    };
}
