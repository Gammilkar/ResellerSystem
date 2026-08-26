using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResellerSystem.Domain.Shared.Dto;
using ResellerSystem.Modules.Inventory.Application;

namespace ResellerSystem.Modules.Inventory.Api;

/// <summary>
/// All routes here require BOTH authentication and a valid X-Database-Id
/// (enforced by TenantResolutionMiddleware + IInventoryService's use of
/// ICurrentTenantAccessor.Require()) — this is the first module endpoint
/// in the system, so it's also the first to exercise that combination.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/inventory/purchases")]
public sealed class PurchasesController : ControllerBase
{
    private readonly IInventoryService _service;

    public PurchasesController(IInventoryService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PurchaseDto>>> List(CancellationToken ct) =>
        Ok(await _service.ListPurchasesAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PurchaseDto>> Get(Guid id, CancellationToken ct) =>
        Ok(await _service.GetPurchaseAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<PurchaseDto>> Create([FromBody] CreatePurchaseRequest request, CancellationToken ct)
    {
        var created = await _service.CreatePurchaseAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<PurchaseDto>> Update(Guid id, [FromBody] UpdatePurchaseRequest request, CancellationToken ct) =>
        Ok(await _service.UpdatePurchaseAsync(id, request, ct));
}

[ApiController]
[Authorize]
[Route("api/v1/inventory/items")]
public sealed class ItemsController : ControllerBase
{
    private readonly IInventoryService _service;
    private readonly IInventoryTableReader _tableReader;

    public ItemsController(IInventoryService service, IInventoryTableReader tableReader)
    {
        _service = service;
        _tableReader = tableReader;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ItemDto>>> List([FromQuery] string? status, CancellationToken ct) =>
        Ok(await _service.ListItemsAsync(status, ct));

    /// <summary>Product Specification section 34 — the main Excel-like
    /// Inventory grid: one row per Item with Purchase/Listing/Sale data
    /// flattened in.</summary>
    [HttpGet("table")]
    public async Task<ActionResult<IReadOnlyList<InventoryTableRowDto>>> Table(CancellationToken ct) =>
        Ok(await _tableReader.GetTableAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ItemDto>> Get(Guid id, CancellationToken ct) =>
        Ok(await _service.GetItemAsync(id, ct));

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<ItemDto>> Update(Guid id, [FromBody] UpdateItemRequest request, CancellationToken ct) =>
        Ok(await _service.UpdateItemAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteItemAsync(id, ct);
        return NoContent();
    }
}

/// <summary>"Поставщики" — Product Specification section 76's "справочник"
/// pattern, first applied for real here (a proper CRUD table + screen)
/// rather than the plain-free-text shortcut used elsewhere in this module.</summary>
[ApiController]
[Authorize]
[Route("api/v1/inventory/suppliers")]
public sealed class SuppliersController : ControllerBase
{
    private readonly ISupplierService _service;

    public SuppliersController(ISupplierService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SupplierDto>>> List(CancellationToken ct) =>
        Ok(await _service.ListAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SupplierDto>> Get(Guid id, CancellationToken ct) =>
        Ok(await _service.GetAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<SupplierDto>> Create([FromBody] CreateSupplierRequest request, CancellationToken ct)
    {
        var created = await _service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<SupplierDto>> Update(Guid id, [FromBody] UpdateSupplierRequest request, CancellationToken ct) =>
        Ok(await _service.UpdateAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/purchases")]
    public async Task<ActionResult<IReadOnlyList<SupplierPurchaseHistoryRowDto>>> GetPurchaseHistory(Guid id, CancellationToken ct) =>
        Ok(await _service.GetPurchaseHistoryAsync(id, ct));
}

/// <summary>The full purchase-intake workflow (Product Specification
/// §1-24) — multi-line, allocation-aware. Deliberately routed under
/// "purchases/full" rather than reusing PurchasesController's routes:
/// that controller's GET/POST/PATCH on "api/v1/inventory/purchases" back
/// the older single-item quick-entry form (still used by the grid and
/// Import) and keep their existing shape untouched for backward
/// compatibility — this is a parallel, richer resource, not a
/// replacement.</summary>
[ApiController]
[Authorize]
[Route("api/v1/inventory/purchases/full")]
public sealed class PurchasesFullController : ControllerBase
{
    private readonly IPurchaseService _service;

    public PurchasesFullController(IPurchaseService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PurchaseListRowDto>>> List([FromQuery] PurchaseListFilterRequest filter, CancellationToken ct) =>
        Ok(await _service.ListAsync(filter, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PurchaseDetailDto>> Get(Guid id, CancellationToken ct) =>
        Ok(await _service.GetAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<PurchaseDetailDto>> Create([FromBody] CreatePurchaseFullRequest request, CancellationToken ct)
    {
        var created = await _service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<PurchaseDetailDto>> Update(Guid id, [FromBody] UpdatePurchaseFullRequest request, CancellationToken ct) =>
        Ok(await _service.UpdateAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpPost("preview-allocation")]
    public ActionResult<PurchaseAllocationResult> PreviewAllocation([FromBody] PurchaseAllocationPreviewRequest request) =>
        Ok(_service.PreviewAllocation(request));
}

/// <summary>Product Specification §76's "справочник-конструктор" — one
/// generic reference-list CRUD surface for every picklist this module
/// needs (Purchase Source, Purchase Type, Payment Method, Category,
/// Expense Type; see ReferenceListKeys).</summary>
[ApiController]
[Authorize]
[Route("api/v1/inventory/reference-lists")]
public sealed class ReferenceListsController : ControllerBase
{
    private readonly IReferenceListService _service;

    public ReferenceListsController(IReferenceListService service)
    {
        _service = service;
    }

    [HttpGet("{listKey}")]
    public async Task<ActionResult<IReadOnlyList<ReferenceListValueDto>>> List(string listKey, CancellationToken ct) =>
        Ok(await _service.ListAsync(listKey, ct));

    [HttpPost]
    public async Task<ActionResult<ReferenceListValueDto>> Create([FromBody] CreateReferenceListValueRequest request, CancellationToken ct)
    {
        var created = await _service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(List), new { listKey = created.ListKey }, created);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }
}
