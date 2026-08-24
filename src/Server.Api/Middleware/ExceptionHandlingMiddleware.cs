using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ResellerSystem.Domain.Shared.Dto;
using ResellerSystem.Server.Application.Exceptions;

namespace ResellerSystem.Server.Api.Middleware;

/// <summary>
/// Single place that turns exceptions into the uniform ApiErrorResponse
/// envelope. Stack traces and internal details are never sent to the
/// client outside Development.
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger, IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleAsync(context, ex);
        }
    }

    private async Task HandleAsync(HttpContext context, Exception exception)
    {
        var traceId = context.TraceIdentifier;

        var (statusCode, code, message, details) = exception switch
        {
            NotFoundException nf => (HttpStatusCode.NotFound, nf.Code, nf.Message, Array.Empty<string>()),
            ValidationFailedException vf => (HttpStatusCode.BadRequest, vf.Code, vf.Message, vf.Details.ToArray()),
            ConflictException cf => (HttpStatusCode.Conflict, cf.Code, cf.Message, Array.Empty<string>()),
            DatabaseNotReadyException dnr => (HttpStatusCode.Conflict, dnr.Code, dnr.Message, Array.Empty<string>()),
            AppException ae => (HttpStatusCode.BadRequest, ae.Code, ae.Message, Array.Empty<string>()),
            _ => (HttpStatusCode.InternalServerError, "INTERNAL_ERROR",
                  _environment.IsDevelopment() ? exception.Message : "An unexpected error occurred.",
                  Array.Empty<string>())
        };

        if (statusCode == HttpStatusCode.InternalServerError)
        {
            _logger.LogError(exception, "Unhandled exception. TraceId: {TraceId}", traceId);
        }
        else
        {
            _logger.LogWarning("Handled exception {Code}: {Message}. TraceId: {TraceId}", code, message, traceId);
        }

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var payload = new ApiErrorResponse
        {
            Error = new ApiError
            {
                Code = code,
                Message = message,
                Details = details,
                TraceId = traceId
            }
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }));
    }
}
