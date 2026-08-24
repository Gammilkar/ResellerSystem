using ResellerSystem.Server.Application.Databases;

namespace ResellerSystem.Server.Api.Middleware;

/// <summary>
/// Reads "X-Database-Id" (if present) and resolves it through
/// IDatabaseContextResolver — which checks existence, Ready status, and
/// (in the future) per-user access — before any module endpoint runs.
/// Requests without the header, or for Core endpoints that don't need a
/// tenant (Health/Version/Databases/Auth/Backups/Updates), simply get
/// ICurrentTenantAccessor.Current == null and proceed; module endpoints
/// that need a tenant call ICurrentTenantAccessor.Require() themselves and
/// get a clear 409 DATABASE_NOT_READY via the existing exception
/// middleware if it's missing/invalid — never a silent wrong-database read.
/// </summary>
public sealed class TenantResolutionMiddleware
{
    private const string HeaderName = "X-Database-Id";

    private readonly RequestDelegate _next;

    public TenantResolutionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IDatabaseContextResolver resolver, ICurrentTenantAccessor tenantAccessor)
    {
        if (context.Request.Headers.TryGetValue(HeaderName, out var headerValue) &&
            Guid.TryParse(headerValue.ToString(), out var databaseId))
        {
            // Let resolution failures flow through as their normal typed
            // exceptions (NotFoundException / DatabaseNotReadyException) —
            // the existing ExceptionHandlingMiddleware already converts
            // those to the uniform ApiErrorResponse format.
            tenantAccessor.Current = await resolver.ResolveAsync(databaseId, context.RequestAborted);
        }

        await _next(context);
    }
}
