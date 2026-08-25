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
    public async Task<ActionResult<IReadOnlyList<ListingDto>>> List(CancellationToken ct) =>
        Ok(await _service.ListListingsAsync(ct));

    [HttpPost]
    public async Task<ActionResult<ListingDto>> Create([FromBody] CreateListingRequest request, CancellationToken ct) =>
        Ok(await _service.CreateListingAsync(request, ct));
}

[ApiController]
[Authorize]
[Route("api/v1/sales")]
public sealed class SalesController : ControllerBase
{
    private readonly ISalesService _service;
    public SalesController(ISalesService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SaleDto>>> List(CancellationToken ct) =>
        Ok(await _service.ListSalesAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SaleDto>> Get(Guid id, CancellationToken ct) =>
        Ok(await _service.GetSaleAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<SaleDto>> Create([FromBody] CreateSaleRequest request, CancellationToken ct)
    {
        var created = await _service.CreateSaleAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

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
    public async Task<ActionResult<IReadOnlyList<ReturnDto>>> List(CancellationToken ct) =>
        Ok(await _service.ListReturnsAsync(ct));

    [HttpPost]
    public async Task<ActionResult<ReturnDto>> Create([FromBody] CreateReturnRequest request, CancellationToken ct) =>
        Ok(await _service.CreateReturnAsync(request, ct));
}
