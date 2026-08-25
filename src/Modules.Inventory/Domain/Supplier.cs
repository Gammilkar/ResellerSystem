namespace ResellerSystem.Modules.Inventory.Domain;

public sealed class Supplier
{
    public Guid Id { get; private set; }
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? Notes { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public string CreatedBy { get; set; } = "system";
    public DateTimeOffset UpdatedAt { get; set; }
    public string UpdatedBy { get; set; } = "system";
    public DateTimeOffset? DeletedAt { get; set; }

    private Supplier() { } // EF Core

    public static Supplier CreateNew(string name, string? phone, string? email, string? address, string? notes)
    {
        var now = DateTimeOffset.UtcNow;
        return new Supplier
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Phone = phone,
            Email = email,
            Address = address,
            Notes = notes,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void Touch() => UpdatedAt = DateTimeOffset.UtcNow;

    public void SoftDelete() => DeletedAt = DateTimeOffset.UtcNow;
}
