using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResellerSystem.Domain.Shared.Dto;
using ResellerSystem.Modules.Reports.Application;

namespace ResellerSystem.Modules.Reports.Api;

[ApiController]
[Authorize]
[Route("api/v1/reports")]
public sealed class ReportsController : ControllerBase
{
    private readonly IReportsService _service;
    public ReportsController(IReportsService service) => _service = service;

    [HttpGet("marketplace-profitability")]
    public async Task<ActionResult<IReadOnlyList<MarketplaceProfitabilityRowDto>>> MarketplaceProfitability(CancellationToken ct) =>
        Ok(await _service.GetMarketplaceProfitabilityAsync(ct));

    [HttpGet("category-profitability")]
    public async Task<ActionResult<IReadOnlyList<CategoryProfitabilityRowDto>>> CategoryProfitability(CancellationToken ct) =>
        Ok(await _service.GetCategoryProfitabilityAsync(ct));

    [HttpGet("inventory-aging")]
    public async Task<ActionResult<IReadOnlyList<InventoryAgingRowDto>>> InventoryAging(CancellationToken ct) =>
        Ok(await _service.GetInventoryAgingAsync(ct));

    [HttpGet("federal-tax-summary")]
    public async Task<ActionResult<FederalTaxSummaryDto>> FederalTaxSummary([FromQuery] int year, CancellationToken ct) =>
        Ok(await _service.GetFederalTaxSummaryAsync(year, ct));

    [HttpGet("1099k-summary")]
    public async Task<ActionResult<IReadOnlyList<Form1099KSummaryDto>>> Form1099KSummary([FromQuery] int year, CancellationToken ct) =>
        Ok(await _service.Get1099KSummaryAsync(year, ct));
}
