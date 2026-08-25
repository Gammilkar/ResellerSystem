using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResellerSystem.Domain.Shared.Dto;
using ResellerSystem.Modules.Dashboard.Application;

namespace ResellerSystem.Modules.Dashboard.Api;

[ApiController]
[Authorize]
[Route("api/v1/dashboard")]
public sealed class DashboardController : ControllerBase
{
    private readonly IDashboardService _service;
    public DashboardController(IDashboardService service) => _service = service;

    [HttpGet("summary")]
    public async Task<ActionResult<DashboardSummaryDto>> Summary(CancellationToken ct) =>
        Ok(await _service.GetSummaryAsync(ct));
}
