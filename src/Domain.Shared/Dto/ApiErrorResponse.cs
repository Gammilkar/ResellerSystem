namespace ResellerSystem.Domain.Shared.Dto;

/// <summary>
/// Uniform error envelope returned by the API. Never includes stack traces
/// or connection details — see Server.Api ExceptionHandlingMiddleware.
/// </summary>
public sealed class ApiErrorResponse
{
    public required ApiError Error { get; init; }
}

public sealed class ApiError
{
    public required string Code { get; init; }
    public required string Message { get; init; }
    public IReadOnlyList<string> Details { get; init; } = Array.Empty<string>();
    public string? TraceId { get; init; }
}
