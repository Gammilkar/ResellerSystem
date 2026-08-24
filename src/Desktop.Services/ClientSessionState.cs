using ResellerSystem.Domain.Shared.Dto;

namespace ResellerSystem.Desktop.Services;

/// <summary>
/// In-memory session state for the currently connected server and selected
/// database. Nothing here is persisted to disk in Stage 1 (no local
/// storage of credentials — there are none to store, since the client never
/// receives PostgreSQL credentials in the first place).
/// </summary>
public sealed class ClientSessionState
{
    public string? ServerAddress { get; set; }
    public string? SessionToken { get; set; }
    public DatabaseProfileDto? SelectedDatabase { get; set; }

    public bool IsConnected => !string.IsNullOrWhiteSpace(ServerAddress);
    public bool IsAuthenticated => !string.IsNullOrWhiteSpace(SessionToken);
    public bool HasSelectedDatabase => SelectedDatabase is not null;
}
