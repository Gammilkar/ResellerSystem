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

    public Task<IReadOnlyList<ItemDto>> ListItemsAsync(string? status, CancellationToken ct = default) =>
        SendAsync<IReadOnlyList<ItemDto>>(HttpMethod.Get,
            string.IsNullOrWhiteSpace(status) ? "/api/v1/inventory/items" : $"/api/v1/inventory/items?status={Uri.EscapeDataString(status)}",
            null, ct);

    public Task<ItemDto> UpdateItemAsync(Guid id, UpdateItemRequest request, CancellationToken ct = default) =>
        SendAsync<ItemDto>(HttpMethod.Patch, $"/api/v1/inventory/items/{id}", request, ct);

    public Task<DashboardSummaryDto> GetDashboardSummaryAsync(CancellationToken ct = default) =>
        SendAsync<DashboardSummaryDto>(HttpMethod.Get, "/api/v1/dashboard/summary", null, ct);

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
