using ResellerSystem.Domain.Shared.Dto;

namespace ResellerSystem.Desktop.Services.Api;

/// <summary>
/// Everything the desktop client can do — talks only to Server API endpoints.
/// The client never sees PostgreSQL credentials, connection strings, or
/// physical database names; it only ever knows a server address, session
/// tokens, and Database Ids (Guid).
/// </summary>
public interface IServerApiClient
{
    /// <summary>Base address currently in use, e.g. "http://192.168.1.100:5000".</summary>
    string BaseAddress { get; }

    void Configure(string baseAddress);

    /// <summary>Sets/clears the bearer token attached to subsequent requests.</summary>
    void SetSessionToken(string? token);

    /// <summary>Attaches X-Database-Id to subsequent requests — required by
    /// any module endpoint (e.g. Inventory) that operates on a specific
    /// tenant database. Pass null to clear it.</summary>
    void SetDatabaseId(Guid? databaseId);

    Task<HealthResponse> GetHealthAsync(CancellationToken ct = default);
    Task<VersionResponse> GetVersionAsync(CancellationToken ct = default);

    Task<AuthStatusResponse> GetAuthStatusAsync(CancellationToken ct = default);
    Task<LoginResponse> LoginAsync(string username, string password, bool rememberMe = false, CancellationToken ct = default);
    Task InitialSetupAsync(string username, string password, CancellationToken ct = default);

    /// <summary>Revokes the current session on the server, then clears the
    /// local bearer token (see SetSessionToken). Safe to call even if the
    /// token turns out to already be invalid/expired.</summary>
    Task LogoutAsync(CancellationToken ct = default);

    Task ChangePasswordAsync(string currentPassword, string newPassword, CancellationToken ct = default);

    Task<IReadOnlyList<DatabaseProfileDto>> ListDatabasesAsync(CancellationToken ct = default);
    Task<DatabaseProfileDto> GetDatabaseAsync(Guid id, CancellationToken ct = default);
    Task<DatabaseProfileDto> CreateDatabaseAsync(CreateDatabaseRequest request, CancellationToken ct = default);
    Task<DatabaseProfileDto> UpdateDatabaseAsync(Guid id, UpdateDatabaseRequest request, CancellationToken ct = default);

    Task<IReadOnlyList<BackupManifestDto>> ListBackupsAsync(CancellationToken ct = default);
    Task<BackupManifestDto> CreateBackupAsync(BackupTypeDto type, CancellationToken ct = default);
    Task RestoreBackupAsync(string backupId, CancellationToken ct = default);

    Task<UpdateCheckResultDto> CheckForUpdateAsync(CancellationToken ct = default);
    Task<UpdateInstallResultDto> InstallUpdateAsync(CancellationToken ct = default);

    Task<IReadOnlyList<PurchaseDto>> ListPurchasesAsync(CancellationToken ct = default);
    Task<PurchaseDto> CreatePurchaseAsync(CreatePurchaseRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<ItemDto>> ListItemsAsync(string? status, CancellationToken ct = default);
    Task<ItemDto> UpdateItemAsync(Guid id, UpdateItemRequest request, CancellationToken ct = default);

    Task<DashboardSummaryDto> GetDashboardSummaryAsync(CancellationToken ct = default);

    Task<IReadOnlyList<ImportTargetFieldDto>> GetImportTargetFieldsAsync(CancellationToken ct = default);
    Task<InspectXlsxResultDto> InspectXlsxAsync(string filePath, CancellationToken ct = default);
    Task<ImportBatchDto> UploadXlsxAsync(string filePath, IReadOnlyDictionary<string, string> mapping, CancellationToken ct = default);
    Task<ImportBatchDto> GetImportBatchAsync(Guid batchId, CancellationToken ct = default);
    Task<ConfirmImportResultDto> ConfirmImportAsync(Guid batchId, CancellationToken ct = default);
    Task<IReadOnlyList<ImportMappingTemplateDto>> ListImportMappingTemplatesAsync(string importType, CancellationToken ct = default);
    Task<ImportMappingTemplateDto> SaveImportMappingTemplateAsync(SaveMappingTemplateRequest request, CancellationToken ct = default);
}
