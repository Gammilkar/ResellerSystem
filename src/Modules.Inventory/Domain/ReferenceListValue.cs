namespace ResellerSystem.Modules.Inventory.Domain;

/// <summary>The generic "справочник-конструктор" mechanism (Product
/// Specification section 76): one table, discriminated by ListKey, backs
/// every user-editable picklist this module needs (Purchase Source,
/// Purchase Type, Payment Method, Category, Expense Type) instead of a
/// bespoke table per concept. Values are stored as plain strings on the
/// entities that use them (Purchase.PurchaseType, PurchaseExpenseLine.
/// ExpenseType, etc.) with no FK — deleting a list value only removes it
/// from future picklists, matching the denormalized-snapshot precedent
/// already established by Purchase.SourceName. Scoped to Modules.Inventory
/// only: a future module needing its own picklists (e.g. Sales' Marketplace)
/// gets its own such table, never a cross-module read of this one.</summary>
public static class ReferenceListKeys
{
    public const string PurchaseSource = "PurchaseSource";
    public const string PurchaseType = "PurchaseType";
    public const string PaymentMethod = "PaymentMethod";
    public const string Category = "Category";
    public const string ExpenseType = "ExpenseType";
}

public sealed class ReferenceListValue
{
    public Guid Id { get; private set; }
    public string ListKey { get; private set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsSystemDefault { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }
    public string CreatedBy { get; set; } = "system";
    public DateTimeOffset UpdatedAt { get; set; }
    public string UpdatedBy { get; set; } = "system";
    public DateTimeOffset? DeletedAt { get; set; }

    private ReferenceListValue() { } // EF Core

    public static ReferenceListValue CreateNew(string listKey, string value, int sortOrder, bool isSystemDefault = false)
    {
        var now = DateTimeOffset.UtcNow;
        return new ReferenceListValue
        {
            Id = Guid.NewGuid(),
            ListKey = listKey,
            Value = value.Trim(),
            SortOrder = sortOrder,
            IsSystemDefault = isSystemDefault,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void Touch() => UpdatedAt = DateTimeOffset.UtcNow;

    public void SoftDelete() => DeletedAt = DateTimeOffset.UtcNow;
}
