using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ResellerSystem.Domain.Shared.Dto;
using ResellerSystem.Modules.Import.Application;

namespace ResellerSystem.Modules.Import.Api;

[ApiController]
[Authorize]
[Route("api/v1/import")]
public sealed class ImportController : ControllerBase
{
    private readonly IImportService _service;
    public ImportController(IImportService service) => _service = service;

    [HttpPost("csv/upload")]
    [RequestSizeLimit(50_000_000)]
    public async Task<ActionResult<ImportBatchDto>> UploadCsv(IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        return Ok(await _service.UploadCsvAsync(stream, file.FileName, ct));
    }

    [HttpGet("batches/{id:guid}")]
    public async Task<ActionResult<ImportBatchDto>> GetBatch(Guid id, CancellationToken ct) =>
        Ok(await _service.GetBatchAsync(id, ct));

    [HttpPost("batches/{id:guid}/confirm")]
    public async Task<ActionResult<ConfirmImportResultDto>> Confirm(Guid id, CancellationToken ct) =>
        Ok(await _service.ConfirmAsync(id, ct));
}
