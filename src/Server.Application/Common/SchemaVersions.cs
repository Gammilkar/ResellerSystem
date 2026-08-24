namespace ResellerSystem.Server.Application.Common;

/// <summary>
/// Single source of truth for the current expected schema version of the
/// master and tenant databases. Server.Data's migration runner applies
/// scripts up to these versions; Server.Application/Api reads them for
/// /health and /api/v1/version without depending on Server.Data (which
/// depends on Server.Application, not the other way around).
///
/// Bump these constants whenever a new migration script is added under
/// Server.Data/Migrations/Scripts/{Master|Tenant}.
/// </summary>
public static class SchemaVersions
{
    public const int MasterCurrentVersion = 1;
    public const int TenantCurrentVersion = 1;
}
