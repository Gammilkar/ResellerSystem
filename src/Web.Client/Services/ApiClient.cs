using System.Net.Http.Headers;
using System.Net.Http.Json;
using ResellerSystem.Domain.Shared.Dto;

namespace ResellerSystem.Web.Client.Services;

/// <summary>Thin exception wrapper around a failed API call — mirrors
/// ServerApiException on the desktop client, just for the web client's
/// smaller surface.</summary>
public sealed class ApiException : Exception
{
    public ApiError Error { get; }
    public ApiException(ApiError error) : base(error.Message) => Error = error;
}

/// <summary>
/// Calls the same /api/v1/... REST endpoints the desktop app calls,
/// against an HttpClient pointed at this same server process (registered
/// in Server.Host/Program.cs with BaseAddress derived from ServerOptions.
/// BindAddress). Deliberately not a reuse of Desktop.Services.
/// IServerApiClient — see the approved plan's reasoning: that interface is
/// shaped for Avalonia commands, a small Blazor-idiomatic client is
/// simpler than forcing reuse.
/// </summary>
public sealed class ApiClient
{
    private readonly HttpClient _http;
    private readonly CircuitState _circuit;

    public ApiClient(HttpClient http, CircuitState circuit)
    {
        _http = http;
        _circuit = circuit;
    }

    private HttpRequestMessage NewRequest(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, path);
        if (_circuit.Token is { } token)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        if (_circuit.DatabaseId is { } databaseId)
        {
            request.Headers.Add("X-Database-Id", databaseId.ToString());
        }
        return request;
    }

    private async Task<T> SendAsync<T>(HttpMethod method, string path, object? body = null, CancellationToken ct = default)
    {
        var request = NewRequest(method, path);
        if (body is not null) request.Content = JsonContent.Create(body);

        using var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>(cancellationToken: ct);
            throw new ApiException(error?.Error ?? new ApiError { Code = "UNKNOWN", Message = $"HTTP {(int)response.StatusCode}" });
        }
        return (await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct))!;
    }

    public Task<LoginResponse> LoginAsync(string username, string password, CancellationToken ct = default) =>
        SendAsync<LoginResponse>(HttpMethod.Post, "/api/v1/auth/login", new LoginRequest { Username = username, Password = password }, ct);

    public Task<IReadOnlyList<DatabaseProfileDto>> ListDatabasesAsync(CancellationToken ct = default) =>
        SendAsync<IReadOnlyList<DatabaseProfileDto>>(HttpMethod.Get, "/api/v1/databases", ct: ct);

    public Task<DashboardSummaryDto> GetDashboardSummaryAsync(CancellationToken ct = default) =>
        SendAsync<DashboardSummaryDto>(HttpMethod.Get, "/api/v1/dashboard/summary", ct: ct);

    public Task<IReadOnlyList<InventoryTableRowDto>> GetInventoryTableAsync(CancellationToken ct = default) =>
        SendAsync<IReadOnlyList<InventoryTableRowDto>>(HttpMethod.Get, "/api/v1/inventory/items/table", ct: ct);

    public Task<ItemDto> UpdateItemAsync(Guid itemId, UpdateItemRequest request, CancellationToken ct = default) =>
        SendAsync<ItemDto>(HttpMethod.Patch, $"/api/v1/inventory/items/{itemId}", request, ct);
}
