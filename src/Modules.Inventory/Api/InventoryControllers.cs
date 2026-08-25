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
