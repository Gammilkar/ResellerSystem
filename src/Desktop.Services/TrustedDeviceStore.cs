using System.Security.Cryptography;
using System.Text.Json;

namespace ResellerSystem.Desktop.Services;

/// <summary>A session persisted locally so the app can skip the login
/// screen on next launch — "Trust this device" on the sign-in form.</summary>
public sealed record TrustedDeviceSession(string ServerAddress, string Token, DateTimeOffset ExpiresAt);

public interface ITrustedDeviceStore
{
    /// <summary>Returns the saved session, or null if there isn't one, it's
    /// expired, or it couldn't be read (e.g. copied to a different Windows
    /// user/machine — DPAPI ties the encryption to both).</summary>
    TrustedDeviceSession? Load();
    void Save(TrustedDeviceSession session);
    void Clear();
}

/// <summary>
/// Windows-only for now (DPAPI). Not persisting on other platforms is a
/// safe no-op — the app just always shows the sign-in form there, which is
/// exactly today's behavior everywhere. See Product Specification section
/// 15: architecture must not need rework to add this for macOS later —
/// swapping this class for one backed by macOS Keychain behind the same
/// ITrustedDeviceStore interface is all that would take.
/// </summary>
public sealed class TrustedDeviceStore : ITrustedDeviceStore
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ResellerSystem Client", "trusted-device.dat");

    // Not a secret by itself (it ships inside the app) — just extra
    // assurance the blob can't be silently reused by another app that also
    // calls DPAPI with no entropy.
    private static readonly byte[] Entropy = "ResellerSystem.TrustedDevice.v1"u8.ToArray();

    public TrustedDeviceSession? Load()
    {
        if (!OperatingSystem.IsWindows() || !File.Exists(FilePath)) return null;

        try
        {
            var encrypted = File.ReadAllBytes(FilePath);
            var json = ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser);
            var session = JsonSerializer.Deserialize<TrustedDeviceSession>(json);

            if (session is null || session.ExpiresAt <= DateTimeOffset.UtcNow)
            {
                Clear();
                return null;
            }

            return session;
        }
        catch
        {
            // Corrupt/foreign-encrypted/unreadable file — treat as "not
            // trusted" rather than surfacing a crash on startup.
            Clear();
            return null;
        }
    }

    public void Save(TrustedDeviceSession session)
    {
        if (!OperatingSystem.IsWindows()) return;

        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var json = JsonSerializer.SerializeToUtf8Bytes(session);
        var encrypted = ProtectedData.Protect(json, Entropy, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(FilePath, encrypted);
    }

    public void Clear()
    {
        try { if (File.Exists(FilePath)) File.Delete(FilePath); } catch { /* best effort */ }
    }
}
