namespace ResellerSystem.Server.Domain.Entities;

/// <summary>
/// A local server user. Single-tenant-of-users model for now (no roles) —
/// see 0003_users.sql. Password is never stored or compared in plaintext;
/// PasswordHash/PasswordSalt are opaque to everything except PasswordHasher.
/// </summary>
public sealed class User : AuditableEntity
{
    public Guid Id { get; private set; }
    public string Username { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string PasswordSalt { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;

    private User() { } // EF Core

    public static User CreateNew(string username, string passwordHash, string passwordSalt)
    {
        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("Username is required.", nameof(username));

        var now = DateTimeOffset.UtcNow;
        return new User
        {
            Id = Guid.NewGuid(),
            Username = username.Trim(),
            PasswordHash = passwordHash,
            PasswordSalt = passwordSalt,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void ChangePassword(string newHash, string newSalt)
    {
        PasswordHash = newHash;
        PasswordSalt = newSalt;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
