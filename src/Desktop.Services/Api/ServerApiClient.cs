using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ResellerSystem.Domain.Shared.Dto;

namespace ResellerSystem.Desktop.Services.Api;

public sealed class ServerApiClient : IServerApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;

    public string BaseAddress { get; private set; } = string.Empty;

    public ServerApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public void Configure(string baseAddress)
    {
        BaseAddress = baseAddress.TrimEnd('/');
        _httpClient.BaseAddress = new Uri(BaseAddress);
        _httpClient.Timeout = TimeSpan.FromSeconds(10);
    }

    public void SetSessionToken(string? token)
    {
        _httpClient.DefaultRequestHeaders.Authorization =
            token is null ? null : new AuthenticationHeaderValue("Bearer", token);
    }

    public void SetDatabaseId(Guid? databaseId)
    {
        _httpClient.DefaultRequestHeaders.Remove("X-Database-Id");
        if (databaseId is not null)
        {
            _httpClient.DefaultRequestHeaders.Add("X-Database-Id", databaseId.Value.ToString());
        }
    }

    public Task<HealthResponse> GetHealthAsync(CancellationToken ct = default) =>
        SendAsync<HealthResponse>(HttpMethod.Get, "/health", null, ct);

    public Task<VersionResponse> GetVersionAsync(CancellationToken ct = default) =>
        SendAsync<VersionResponse>(HttpMethod.Get, "/api/v1/version", null, ct);

    public Task<AuthStatusResponse> GetAuthStatusAsync(CancellationToken ct = default) =>
        SendAsync<AuthStatusResponse>(HttpMethod.Get, "/api/v1/auth/status", null, ct);

    public async Task<LoginResponse> LoginAsync(string username, string password, bool rememberMe = false, CancellationToken ct = default)
    {
        var response = await SendAsync<LoginResponse>(HttpMethod.Post, "/api/v1/auth/login",
            new LoginRequest { Username = username, Password = password, RememberMe = rememberMe }, ct);
        SetSessionToken(response.Token);
        return response;
    }

    public Task InitialSetupAsync(string username, string password, CancellationToken ct = default) =>
        SendNoContentAsync(HttpMethod.Post, "/api/v1/auth/setup",
            new InitialSetupRequest { Username = username, Password = password }, ct);

    public async Task LogoutAsync(CancellationToken ct = default)
    {
        try
        {
            await SendNoContentAsync(HttpMethod.Post, "/api/v1/auth/logout", null, ct);
        }
        catch (ServerApiException)
        {
            // Token already invalid/expired server-side — fine, we're
            // clearing it locally regardless.
        }
        finally
        {
            SetSessionToken(null);
        }
    }

    public Task ChangePasswordAsync(string currentPassword, string newPassword, CancellationToken ct = default) =>
        SendNoContentAsync(HttpMethod.Post, "/api/v1/auth/change-password",
            new ChangePasswordRequest { CurrentPassword = currentPassword, NewPassword = newPassword }, ct);

    public Task<IReadOnlyList<DatabaseProfileDto>> ListDatabasesAsync(CancellationToken ct = default) =>
        SendAsync<IReadOnlyList<DatabaseProfileDto>>(HttpMethod.Get, "/api/v1/databases", null, ct);

    public Task<DatabaseProfileDto> GetDatabaseAsync(Guid id, CancellationToken ct = default) =>
        SendAsync<DatabaseProfileDto>(HttpMethod.Get, $"/api/v1/databases/{id}", null, ct);

    public Task<DatabaseProfileDto> CreateDatabaseAsync(CreateDatabaseRequest request, CancellationToken ct = default) =>
        SendAsync<DatabaseProfileDto>(HttpMethod.Post, "/api/v1/databases", request, ct);

    public Task<DatabaseProfileDto> UpdateDatabaseAsync(Guid id, UpdateDatabaseRequest request, CancellationToken ct = default) =>
        SendAsync<DatabaseProfileDto>(HttpMethod.Patch, $"/api/v1/databases/{id}", request, ct);

    public Task<IReadOnlyList<BackupManifestDto>> ListBackupsAsync(CancellationToken ct = default) =>
        SendAsync<IReadOnlyList<BackupManifestDto>>(HttpMethod.Get, "/api/v1/backups", null, ct);

    public Task<BackupManifestDto> CreateBackupAsync(BackupTypeDto type, CancellationToken ct = default) =>
        SendAsync<BackupManifestDto>(HttpMethod.Post, "/api/v1/backups", new CreateBackupRequest { Type = type }, ct);

    public Task RestoreBackupAsync(string backupId, CancellationToken ct = default) =>
        SendNoContentAsync(HttpMethod.Post, $"/api/v1/backups/{backupId}/restore", null, ct);

    public Task<UpdateCheckResultDto> CheckForUpdateAsync(CancellationToken ct = default) =>
        SendAsync<UpdateCheckResultDto>(HttpMethod.Get, "/api/v1/updates/check", null, ct);

    public Task<UpdateInstallResultDto> InstallUpdateAsync(CancellationToken ct = default) =>
        SendAsync<UpdateInstallResultDto>(HttpMethod.Post, "/api/v1/updates/install", null, ct);

    public Task<IReadOnlyList<PurchaseDto>> ListPurchasesAsync(CancellationToken ct = default) =>
        SendAsync<IReadOnlyList<PurchaseDto>>(HttpMethod.Get, "/api/v1/inventory/purchases", null, ct);

    public Task<PurchaseDto> CreatePurchaseAsync(CreatePurchaseRequest request, CancellationToken ct = default) =>
        SendAsync<PurchaseDto>(HttpMethod.Post, "/api/v1/inventory/purchases", request, ct);

    public Task<PurchaseDto> GetPurchaseAsync(Guid id, CancellationToken ct = default) =>
        SendAsync<PurchaseDto>(HttpMethod.Get, $"/api/v1/inventory/purchases/{id}", null, ct);

    public Task<PurchaseDto> UpdatePurchaseAsync(Guid id, UpdatePurchaseRequest request, CancellationToken ct = default) =>
        SendAsync<PurchaseDto>(HttpMethod.Patch, $"/api/v1/inventory/purchases/{id}", request, ct);

    public Task<IReadOnlyList<ItemDto>> ListItemsAsync(string? status, CancellationToken ct = default) =>
        SendAsync<IReadOnlyList<ItemDto>>(HttpMethod.Get,
            string.IsNullOrWhiteSpace(status) ? "/api/v1/inventory/items" : $"/api/v1/inventory/items?status={Uri.EscapeDataString(status)}",
            null, ct);

    public Task<ItemDto> GetItemAsync(Guid id, CancellationToken ct = default) =>
        SendAsync<ItemDto>(HttpMethod.Get, $"/api/v1/inventory/items/{id}", null, ct);

    public Task<ItemDto> UpdateItemAsync(Guid id, UpdateItemRequest request, CancellationToken ct = default) =>
        SendAsync<ItemDto>(HttpMethod.Patch, $"/api/v1/inventory/items/{id}", request, ct);

    public Task DeleteItemAsync(Guid id, CancellationToken ct = default) =>
        SendNoContentAsync(HttpMethod.Delete, $"/api/v1/inventory/items/{id}", null, ct);

    public Task<IReadOnlyList<InventoryTableRowDto>> ListInventoryTableAsync(CancellationToken ct = default) =>
        SendAsync<IReadOnlyList<InventoryTableRowDto>>(HttpMethod.Get, "/api/v1/inventory/items/table", null, ct);

    public Task<IReadOnlyList<PurchaseListRowDto>> ListPurchasesFullAsync(PurchaseListFilterRequest? filter = null, CancellationToken ct = default)
    {
        var query = new List<string>();
        if (filter?.DateFrom is { } dateFrom) query.Add($"dateFrom={dateFrom:yyyy-MM-dd}");
        if (filter?.DateTo is { } dateTo) query.Add($"dateTo={dateTo:yyyy-MM-dd}");
        if (!string.IsNullOrWhiteSpace(filter?.SourceName)) query.Add($"sourceName={Uri.EscapeDataString(filter.SourceName)}");
        if (!string.IsNullOrWhiteSpace(filter?.PurchaseType)) query.Add($"purchaseType={Uri.EscapeDataString(filter.PurchaseType)}");
        if (filter?.UsedResellerPermit is { } permit) query.Add($"usedResellerPermit={permit}");
        if (!string.IsNullOrWhiteSpace(filter?.PaymentMethod)) query.Add($"paymentMethod={Uri.EscapeDataString(filter.PaymentMethod)}");
        if (filter?.MinTotalAmount is { } min) query.Add($"minTotalAmount={min}");
        if (filter?.MaxTotalAmount is { } max) query.Add($"maxTotalAmount={max}");
        if (!string.IsNullOrWhiteSpace(filter?.Search)) query.Add($"search={Uri.EscapeDataString(filter.Search)}");

        var path = "/api/v1/inventory/purchases/full" + (query.Count > 0 ? "?" + string.Join("&", query) : string.Empty);
        return SendAsync<IReadOnlyList<PurchaseListRowDto>>(HttpMethod.Get, path, null, ct);
    }

    public Task<PurchaseDetailDto> GetPurchaseFullAsync(Guid id, CancellationToken ct = default) =>
        SendAsync<PurchaseDetailDto>(HttpMethod.Get, $"/api/v1/inventory/purchases/full/{id}", null, ct);

    public Task<PurchaseDetailDto> CreatePurchaseFullAsync(CreatePurchaseFullRequest request, CancellationToken ct = default) =>
        SendAsync<PurchaseDetailDto>(HttpMethod.Post, "/api/v1/inventory/purchases/full", request, ct);

    public Task<PurchaseDetailDto> UpdatePurchaseFullAsync(Guid id, UpdatePurchaseFullRequest request, CancellationToken ct = default) =>
        SendAsync<PurchaseDetailDto>(HttpMethod.Patch, $"/api/v1/inventory/purchases/full/{id}", request, ct);

    public Task DeletePurchaseFullAsync(Guid id, CancellationToken ct = default) =>
        SendNoContentAsync(HttpMethod.Delete, $"/api/v1/inventory/purchases/full/{id}", null, ct);

    public Task<PurchaseAllocationResult> PreviewPurchaseAllocationAsync(PurchaseAllocationPreviewRequest request, CancellationToken ct = default) =>
        SendAsync<PurchaseAllocationResult>(HttpMethod.Post, "/api/v1/inventory/purchases/full/preview-allocation", request, ct);

    public Task<IReadOnlyList<ReferenceListValueDto>> ListReferenceValuesAsync(string listKey, CancellationToken ct = default) =>
        SendAsync<IReadOnlyList<ReferenceListValueDto>>(HttpMethod.Get, $"/api/v1/inventory/reference-lists/{Uri.EscapeDataString(listKey)}", null, ct);

    public Task<ReferenceListValueDto> CreateReferenceValueAsync(CreateReferenceListValueRequest request, CancellationToken ct = default) =>
        SendAsync<ReferenceListValueDto>(HttpMethod.Post, "/api/v1/inventory/reference-lists", request, ct);

    public Task DeleteReferenceValueAsync(Guid id, CancellationToken ct = default) =>
        SendNoContentAsync(HttpMethod.Delete, $"/api/v1/inventory/reference-lists/{id}", null, ct);

    public Task<IReadOnlyList<ListingDto>> ListListingsAsync(Guid? itemId = null, CancellationToken ct = default) =>
        SendAsync<IReadOnlyList<ListingDto>>(HttpMethod.Get,
            itemId is null ? "/api/v1/sales/listings" : $"/api/v1/sales/listings?itemId={itemId}", null, ct);

    public Task<ListingDto> CreateListingAsync(CreateListingRequest request, CancellationToken ct = default) =>
        SendAsync<ListingDto>(HttpMethod.Post, "/api/v1/sales/listings", request, ct);

    public Task<ListingDto> UpdateListingAsync(Guid id, UpdateListingRequest request, CancellationToken ct = default) =>
        SendAsync<ListingDto>(HttpMethod.Patch, $"/api/v1/sales/listings/{id}", request, ct);

    public Task<IReadOnlyList<SaleDto>> ListSalesAsync(Guid? itemId = null, CancellationToken ct = default) =>
        SendAsync<IReadOnlyList<SaleDto>>(HttpMethod.Get,
            itemId is null ? "/api/v1/sales" : $"/api/v1/sales?itemId={itemId}", null, ct);

    public Task<SaleDto> CreateSaleAsync(CreateSaleRequest request, CancellationToken ct = default) =>
        SendAsync<SaleDto>(HttpMethod.Post, "/api/v1/sales", request, ct);

    public Task<SaleDto> UpdateSaleAsync(Guid id, UpdateSaleRequest request, CancellationToken ct = default) =>
        SendAsync<SaleDto>(HttpMethod.Patch, $"/api/v1/sales/{id}", request, ct);

    public Task<SaleFeeDto> AddSaleFeeAsync(Guid saleId, CreateSaleFeeRequest request, CancellationToken ct = default) =>
        SendAsync<SaleFeeDto>(HttpMethod.Post, $"/api/v1/sales/{saleId}/fees", request, ct);

    public Task<SaleFinancialsDto> GetSaleFinancialsAsync(Guid saleId, CancellationToken ct = default) =>
        SendAsync<SaleFinancialsDto>(HttpMethod.Get, $"/api/v1/sales/{saleId}/financials", null, ct);

    public Task<IReadOnlyList<ReturnDto>> ListReturnsAsync(Guid? itemId = null, CancellationToken ct = default) =>
        SendAsync<IReadOnlyList<ReturnDto>>(HttpMethod.Get,
            itemId is null ? "/api/v1/sales/returns" : $"/api/v1/sales/returns?itemId={itemId}", null, ct);

    public Task<ReturnDto> CreateReturnAsync(CreateReturnRequest request, CancellationToken ct = default) =>
        SendAsync<ReturnDto>(HttpMethod.Post, "/api/v1/sales/returns", request, ct);

    public Task<IReadOnlyList<SupplierDto>> ListSuppliersAsync(CancellationToken ct = default) =>
        SendAsync<IReadOnlyList<SupplierDto>>(HttpMethod.Get, "/api/v1/inventory/suppliers", null, ct);

    public Task<SupplierDto> GetSupplierAsync(Guid id, CancellationToken ct = default) =>
        SendAsync<SupplierDto>(HttpMethod.Get, $"/api/v1/inventory/suppliers/{id}", null, ct);

    public Task<SupplierDto> CreateSupplierAsync(CreateSupplierRequest request, CancellationToken ct = default) =>
        SendAsync<SupplierDto>(HttpMethod.Post, "/api/v1/inventory/suppliers", request, ct);

    public Task<SupplierDto> UpdateSupplierAsync(Guid id, UpdateSupplierRequest request, CancellationToken ct = default) =>
        SendAsync<SupplierDto>(HttpMethod.Patch, $"/api/v1/inventory/suppliers/{id}", request, ct);

    public Task DeleteSupplierAsync(Guid id, CancellationToken ct = default) =>
        SendNoContentAsync(HttpMethod.Delete, $"/api/v1/inventory/suppliers/{id}", null, ct);

    public Task<IReadOnlyList<SupplierPurchaseHistoryRowDto>> GetSupplierPurchaseHistoryAsync(Guid id, CancellationToken ct = default) =>
        SendAsync<IReadOnlyList<SupplierPurchaseHistoryRowDto>>(HttpMethod.Get, $"/api/v1/inventory/suppliers/{id}/purchases", null, ct);

    public Task<IReadOnlyList<ExpenseDto>> ListExpensesAsync(Guid? itemId = null, Guid? purchaseId = null, Guid? saleId = null, CancellationToken ct = default)
    {
        var query = new List<string>();
        if (itemId is not null) query.Add($"itemId={itemId}");
        if (purchaseId is not null) query.Add($"purchaseId={purchaseId}");
        if (saleId is not null) query.Add($"saleId={saleId}");
        var path = "/api/v1/expenses" + (query.Count > 0 ? "?" + string.Join("&", query) : string.Empty);
        return SendAsync<IReadOnlyList<ExpenseDto>>(HttpMethod.Get, path, null, ct);
    }

    public Task<ExpenseDto> CreateExpenseAsync(CreateExpenseRequest request, CancellationToken ct = default) =>
        SendAsync<ExpenseDto>(HttpMethod.Post, "/api/v1/expenses", request, ct);

    public Task DeleteExpenseAsync(Guid id, CancellationToken ct = default) =>
        SendNoContentAsync(HttpMethod.Delete, $"/api/v1/expenses/{id}", null, ct);

    public Task<DocumentDto> UploadDocumentAsync(string filePath, CancellationToken ct = default) =>
        SendFileAsync<DocumentDto>("/api/v1/documents/upload", filePath, null, ct);

    public Task<DocumentDto> LinkDocumentAsync(Guid documentId, string entityType, Guid entityId, CancellationToken ct = default) =>
        SendAsync<DocumentDto>(HttpMethod.Post, $"/api/v1/documents/{documentId}/links",
            new CreateDocumentLinkRequest { EntityType = entityType, EntityId = entityId }, ct);

    public Task<IReadOnlyList<DocumentDto>> ListDocumentsForEntityAsync(string entityType, Guid entityId, CancellationToken ct = default) =>
        SendAsync<IReadOnlyList<DocumentDto>>(HttpMethod.Get, $"/api/v1/documents/for/{Uri.EscapeDataString(entityType)}/{entityId}", null, ct);

    public async Task<(byte[] Content, string? MimeType, string Filename)> DownloadDocumentAsync(Guid documentId, CancellationToken ct = default)
    {
        using var response = await _httpClient.GetAsync($"/api/v1/documents/{documentId}/content", ct);
        if (!response.IsSuccessStatusCode)
        {
            await ThrowApiExceptionAsync(response, ct);
        }

        var bytes = await response.Content.ReadAsByteArrayAsync(ct);
        var mimeType = response.Content.Headers.ContentType?.MediaType;
        var filename = response.Content.Headers.ContentDisposition?.FileNameStar?.Trim('"')
            ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"')
            ?? "document";
        return (bytes, mimeType, filename);
    }

    public Task<IReadOnlyList<AuditLogEntryDto>> GetAuditLogAsync(string? entityType, Guid? entityId, int limit = 200, CancellationToken ct = default)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(entityType)) query.Add($"entityType={Uri.EscapeDataString(entityType)}");
        if (entityId is not null) query.Add($"entityId={entityId}");
        query.Add($"limit={limit}");
        var path = "/api/v1/audit-log?" + string.Join("&", query);
        return SendAsync<IReadOnlyList<AuditLogEntryDto>>(HttpMethod.Get, path, null, ct);
    }

    public Task<DashboardSummaryDto> GetDashboardSummaryAsync(CancellationToken ct = default) =>
        SendAsync<DashboardSummaryDto>(HttpMethod.Get, "/api/v1/dashboard/summary", null, ct);

    public Task<IReadOnlyList<ImportTargetFieldDto>> GetImportTargetFieldsAsync(CancellationToken ct = default) =>
        SendAsync<IReadOnlyList<ImportTargetFieldDto>>(HttpMethod.Get, "/api/v1/import/target-fields", null, ct);

    public Task<InspectXlsxResultDto> InspectXlsxAsync(string filePath, string? sheetName = null, CancellationToken ct = default) =>
        SendFileAsync<InspectXlsxResultDto>("/api/v1/import/xlsx/inspect", filePath,
            sheetName is null ? null : new Dictionary<string, string> { ["sheetName"] = sheetName }, ct);

    public Task<ImportBatchDto> UploadXlsxAsync(string filePath, string? sheetName, IReadOnlyDictionary<string, string> mapping, CancellationToken ct = default)
    {
        var formFields = new Dictionary<string, string> { ["mapping"] = JsonSerializer.Serialize(mapping) };
        if (sheetName is not null) formFields["sheetName"] = sheetName;
        return SendFileAsync<ImportBatchDto>("/api/v1/import/xlsx/upload", filePath, formFields, ct);
    }

    public Task<ImportBatchDto> GetImportBatchAsync(Guid batchId, CancellationToken ct = default) =>
        SendAsync<ImportBatchDto>(HttpMethod.Get, $"/api/v1/import/batches/{batchId}", null, ct);

    public Task<ConfirmImportResultDto> ConfirmImportAsync(Guid batchId, CancellationToken ct = default) =>
        SendAsync<ConfirmImportResultDto>(HttpMethod.Post, $"/api/v1/import/batches/{batchId}/confirm", null, ct);

    public Task<IReadOnlyList<ImportMappingTemplateDto>> ListImportMappingTemplatesAsync(string importType, CancellationToken ct = default) =>
        SendAsync<IReadOnlyList<ImportMappingTemplateDto>>(HttpMethod.Get, $"/api/v1/import/mapping-templates?importType={Uri.EscapeDataString(importType)}", null, ct);

    public Task<ImportMappingTemplateDto> SaveImportMappingTemplateAsync(SaveMappingTemplateRequest request, CancellationToken ct = default) =>
        SendAsync<ImportMappingTemplateDto>(HttpMethod.Post, "/api/v1/import/mapping-templates", request, ct);

    private async Task<TResponse> SendFileAsync<TResponse>(string path, string filePath, IReadOnlyDictionary<string, string>? formFields, CancellationToken ct)
    {
        using var content = new MultipartFormDataContent();
        await using var fileStream = File.OpenRead(filePath);
        using var fileContent = new StreamContent(fileStream);
        content.Add(fileContent, "file", Path.GetFileName(filePath));

        if (formFields is not null)
        {
            foreach (var (key, value) in formFields)
            {
                content.Add(new StringContent(value), key);
            }
        }

        using var response = await _httpClient.PostAsync(path, content, ct);
        if (!response.IsSuccessStatusCode)
        {
            await ThrowApiExceptionAsync(response, ct);
        }

        var result = await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions, ct);
        return result ?? throw new ServerApiException(new ApiError
        {
            Code = "EMPTY_RESPONSE",
            Message = "Server returned an empty response body."
        });
    }

    private async Task SendNoContentAsync(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null) request.Content = JsonContent.Create(body);

        using var response = await _httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            await ThrowApiExceptionAsync(response, ct);
        }
    }

    private async Task<TResponse> SendAsync<TResponse>(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        using var response = await _httpClient.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            await ThrowApiExceptionAsync(response, ct);
        }

        var result = await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions, ct);
        return result ?? throw new ServerApiException(new ApiError
        {
            Code = "EMPTY_RESPONSE",
            Message = "Server returned an empty response body."
        });
    }

    private static async Task ThrowApiExceptionAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var errorPayload = await response.Content.ReadFromJsonAsync<ApiErrorResponse>(JsonOptions, ct);
        if (errorPayload is not null)
        {
            throw new ServerApiException(errorPayload.Error);
        }

        throw new ServerApiException(new ApiError
        {
            Code = "UNKNOWN_ERROR",
            Message = $"Server returned {(int)response.StatusCode} {response.ReasonPhrase}."
        });
    }
}
