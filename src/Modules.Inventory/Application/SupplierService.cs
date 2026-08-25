using Microsoft.EntityFrameworkCore;
using ResellerSystem.Domain.Shared.Dto;
using ResellerSystem.Modules.Inventory.Data;
using ResellerSystem.Modules.Inventory.Domain;
using ResellerSystem.Server.Application.Audit;
using ResellerSystem.Server.Application.Exceptions;
using ResellerSystem.Server.Domain.Abstractions;

namespace ResellerSystem.Modules.Inventory.Application;

public interface ISupplierService
{
    Task<SupplierDto> CreateAsync(CreateSupplierRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<SupplierDto>> ListAsync(CancellationToken ct = default);
    Task<SupplierDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<SupplierDto> UpdateAsync(Guid id, UpdateSupplierRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<SupplierPurchaseHistoryRowDto>> GetPurchaseHistoryAsync(Guid supplierId, CancellationToken ct = default);
}

public sealed class SupplierService : ISupplierService
{
    private readonly IInventoryDbContextFactory _dbContextFactory;
    private readonly IAuditLogger _auditLogger;
    private readonly ICurrentUserContext _currentUser;

    public SupplierService(IInventoryDbContextFactory dbContextFactory, IAuditLogger auditLogger, ICurrentUserContext currentUser)
    {
        _dbContextFactory = dbContextFactory;
        _auditLogger = auditLogger;
        _currentUser = currentUser;
    }

    public async Task<SupplierDto> CreateAsync(CreateSupplierRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ValidationFailedException(new[] { "Supplier name is required." });

        await using var db = _dbContextFactory.CreateForCurrentTenant();

        var supplier = Supplier.CreateNew(request.Name, request.Phone, request.Email, request.Address, request.Notes);
        supplier.CreatedBy = _currentUser.DisplayName;
        supplier.UpdatedBy = _currentUser.DisplayName;
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync(ct);

        await _auditLogger.LogAsync(new AuditEntry("Supplier", supplier.Id, "Created", _currentUser.DisplayName, "manual"), ct);
        return ToDto(supplier);
    }

    public async Task<IReadOnlyList<SupplierDto>> ListAsync(CancellationToken ct = default)
    {
        await using var db = _dbContextFactory.CreateForCurrentTenant();

        return await db.Suppliers
            .OrderBy(s => s.Name)
            .Select(s => ToDto(s))
            .ToListAsync(ct);
    }

    public async Task<SupplierDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = _dbContextFactory.CreateForCurrentTenant();

        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new NotFoundException("SUPPLIER_NOT_FOUND", "Supplier was not found.");

        return ToDto(supplier);
    }

    public async Task<SupplierDto> UpdateAsync(Guid id, UpdateSupplierRequest request, CancellationToken ct = default)
    {
        await using var db = _dbContextFactory.CreateForCurrentTenant();

        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new NotFoundException("SUPPLIER_NOT_FOUND", "Supplier was not found.");

        var username = _currentUser.DisplayName;
        var changes = new List<AuditEntry>();
        void TrackChange(string field, string? oldValue, string? newValue)
        {
            if (oldValue != newValue) changes.Add(new AuditEntry("Supplier", supplier.Id, "Updated", username, "manual", field, oldValue, newValue));
        }

        if (request.Name is not null) { TrackChange("Name", supplier.Name, request.Name.Trim()); supplier.Name = request.Name.Trim(); }
        if (request.Phone is not null) { TrackChange("Phone", supplier.Phone, request.Phone); supplier.Phone = request.Phone; }
        if (request.Email is not null) { TrackChange("Email", supplier.Email, request.Email); supplier.Email = request.Email; }
        if (request.Address is not null) { TrackChange("Address", supplier.Address, request.Address); supplier.Address = request.Address; }
        if (request.Notes is not null) { TrackChange("Notes", supplier.Notes, request.Notes); supplier.Notes = request.Notes; }
        supplier.UpdatedBy = username;
        supplier.Touch();

        await db.SaveChangesAsync(ct);
        if (changes.Count > 0) await _auditLogger.LogManyAsync(changes, ct);

        return ToDto(supplier);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = _dbContextFactory.CreateForCurrentTenant();

        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new NotFoundException("SUPPLIER_NOT_FOUND", "Supplier was not found.");

        supplier.SoftDelete(); // never a hard delete — matches Item/Purchase convention
        await db.SaveChangesAsync(ct);
        await _auditLogger.LogAsync(new AuditEntry("Supplier", supplier.Id, "Deleted", _currentUser.DisplayName, "manual"), ct);
    }

    public async Task<IReadOnlyList<SupplierPurchaseHistoryRowDto>> GetPurchaseHistoryAsync(Guid supplierId, CancellationToken ct = default)
    {
        await using var db = _dbContextFactory.CreateForCurrentTenant();

        return await db.Purchases
            .Where(p => p.SupplierId == supplierId)
            .OrderByDescending(p => p.PurchaseDate)
            .Select(p => new SupplierPurchaseHistoryRowDto
            {
                PurchaseId = p.Id,
                PurchaseDate = p.PurchaseDate,
                TotalAmount = p.TotalAmount,
                ItemCount = p.Items.Count
            })
            .ToListAsync(ct);
    }

    private static SupplierDto ToDto(Supplier s) => new()
    {
        Id = s.Id,
        Name = s.Name,
        Phone = s.Phone,
        Email = s.Email,
        Address = s.Address,
        Notes = s.Notes,
        CreatedAt = s.CreatedAt
    };
}
