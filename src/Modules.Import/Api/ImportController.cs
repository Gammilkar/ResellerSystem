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

    [HttpGet("target-fields")]
    public ActionResult<IReadOnlyList<ImportTargetFieldDto>> GetTargetFields() => Ok(_service.GetTargetFields());

    [HttpPost("xlsx/inspect")]
    [RequestSizeLimit(50_000_000)]
    public async Task<ActionResult<InspectXlsxResultDto>> InspectXlsx(IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        return Ok(await _service.InspectXlsxAsync(stream, ct));
    }

    [HttpPost("xlsx/upload")]
    [RequestSizeLimit(50_000_000)]
    public async Task<ActionResult<ImportBatchDto>> UploadXlsx(IFormFile file, [FromForm] string mapping, CancellationToken ct)
    {
        var mappingDict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(mapping) ?? new();
        await using var stream = file.OpenReadStream();
        return Ok(await _service.UploadXlsxAsync(stream, file.FileName, mappingDict, ct));
    }

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

    [HttpGet("mapping-templates")]
    public async Task<ActionResult<IReadOnlyList<ImportMappingTemplateDto>>> ListMappingTemplates([FromQuery] string importType, CancellationToken ct) =>
        Ok(await _service.ListMappingTemplatesAsync(importType, ct));

    [HttpPost("mapping-templates")]
    public async Task<ActionResult<ImportMappingTemplateDto>> SaveMappingTemplate([FromBody] SaveMappingTemplateRequest request, CancellationToken ct) =>
        Ok(await _service.SaveMappingTemplateAsync(request, ct));
}
