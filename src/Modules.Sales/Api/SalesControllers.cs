using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResellerSystem.Domain.Shared.Dto;
using ResellerSystem.Modules.Sales.Application;

namespace ResellerSystem.Modules.Sales.Api;

[ApiController]
[Authorize]
[Route("api/v1/sales/listings")]
public sealed class ListingsController : ControllerBase
{
    private readonly ISalesService _service;
    public ListingsController(ISalesService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ListingDto>>> List([FromQuery] Guid? itemId, CancellationToken ct) =>
        Ok(await _service.ListListingsAsync(itemId, ct));

    [HttpPost]
    public async Task<ActionResult<ListingDto>> Create([FromBody] CreateListingRequest request, CancellationToken ct) =>
        Ok(await _service.CreateListingAsync(request, ct));

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<ListingDto>> Update(Guid id, [FromBody] UpdateListingRequest request, CancellationToken ct) =>
        Ok(await _service.UpdateListingAsync(id, request, ct));
}

[ApiController]
[Authorize]
[Route("api/v1/sales")]
public sealed class SalesController : ControllerBase
{
    private readonly ISalesService _service;
    public SalesController(ISalesService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SaleDto>>> List([FromQuery] Guid? itemId, CancellationToken ct) =>
        Ok(await _service.ListSalesAsync(itemId, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SaleDto>> Get(Guid id, CancellationToken ct) =>
        Ok(await _service.GetSaleAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<SaleDto>> Create([FromBody] CreateSaleRequest request, CancellationToken ct)
    {
        var created = await _service.CreateSaleAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<SaleDto>> Update(Guid id, [FromBody] UpdateSaleRequest request, CancellationToken ct) =>
        Ok(await _service.UpdateSaleAsync(id, request, ct));

    [HttpPost("{id:guid}/fees")]
    public async Task<ActionResult<SaleFeeDto>> AddFee(Guid id, [FromBody] CreateSaleFeeRequest request, CancellationToken ct) =>
        Ok(await _service.AddFeeAsync(id, request, ct));

    [HttpGet("{id:guid}/financials")]
    public async Task<ActionResult<SaleFinancialsDto>> GetFinancials(Guid id, CancellationToken ct) =>
        Ok(await _service.GetFinancialsAsync(id, ct));
}

[ApiController]
[Authorize]
[Route("api/v1/sales/returns")]
public sealed class ReturnsController : ControllerBase
{
    private readonly ISalesService _service;
    public ReturnsController(ISalesService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ReturnDto>>> List([FromQuery] Guid? itemId, CancellationToken ct) =>
        Ok(await _service.ListReturnsAsync(itemId, ct));

    [HttpPost]
    public async Task<ActionResult<ReturnDto>> Create([FromBody] CreateReturnRequest request, CancellationToken ct) =>
        Ok(await _service.CreateReturnAsync(request, ct));
}
