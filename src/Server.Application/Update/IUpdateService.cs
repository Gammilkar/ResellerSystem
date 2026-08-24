using ResellerSystem.Domain.Shared.Dto;

namespace ResellerSystem.Server.Application.Update;

/// <summary>
/// Core Update Engine. Orchestrates: fetch manifest -> compare versions ->
/// (on install) download package -> verify checksum -> mandatory backup ->
/// hand off to the elevated, out-of-process Server.Updater helper, which
/// performs stop -> apply (side-by-side + junction swap) -> start ->
/// health-check -> automatic file-level rollback on failure. See
/// Product Development Plan v1.0, Part 2.3, and
/// KNOWN_LIMITATIONS.md for exactly what is and isn't automatic yet.
/// </summary>
public interface IUpdateService
{
    Task<UpdateCheckResultDto> CheckForUpdateAsync(CancellationToken ct = default);

    /// <summary>
    /// Kicks off the install flow and returns once the mandatory backup is
    /// complete and the elevated updater process has been launched — it
    /// does NOT wait for the update to finish, because the very process
    /// handling this HTTP request is about to be stopped by that updater.
    /// The caller (Server Manager) polls /health afterwards to observe the
    /// outcome, same as it does for ordinary start/stop.
    /// </summary>
    Task<UpdateInstallResultDto> BeginInstallAsync(CancellationToken ct = default);
}
