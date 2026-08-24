using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ResellerSystem.Domain.Shared.Dto;
using ResellerSystem.Modules.Documents.Application;

namespace ResellerSystem.Modules.Documents.Api;

[ApiController]
[Authorize]
[Route("api/v1/documents")]
public sealed class DocumentsController : ControllerBase
{
    private readonly IDocumentsService _service;
    public DocumentsController(IDocumentsService service) => _service = service;

    [HttpPost("upload")]
    [RequestSizeLimit(500_000_000)] // 500 MB — documents are never compressed/re-encoded (Architecture Plan v0.1 section 9)
    public async Task<ActionResult<DocumentDto>> Upload(IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        var result = await _service.UploadAsync(stream, file.FileName, file.ContentType, ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/links")]
    public async Task<ActionResult<DocumentDto>> Link(Guid id, [FromBody] CreateDocumentLinkRequest request, CancellationToken ct) =>
        Ok(await _service.LinkAsync(id, request, ct));

    [HttpGet("for/{entityType}/{entityId:guid}")]
    public async Task<ActionResult<IReadOnlyList<DocumentDto>>> ListForEntity(string entityType, Guid entityId, CancellationToken ct) =>
        Ok(await _service.ListForEntityAsync(entityType, entityId, ct));

    [HttpGet("{id:guid}/content")]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
    {
        var (content, mimeType, filename) = await _service.DownloadAsync(id, ct);
        return File(content, mimeType, filename);
    }
}
