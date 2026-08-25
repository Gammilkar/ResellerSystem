namespace ResellerSystem.Desktop.ViewModels;

/// <summary>Suggested values for the Marketplace/Место продажи ComboBoxes —
/// both are IsEditable, so these are a convenience list, not a restriction:
/// picking "Other" (or just typing straight over it) lets any custom value
/// through, satisfying "при выборе Other может быть введён произвольный
/// текст" without needing separate transient per-row state for a reveal-a-
/// textbox interaction.</summary>
public static class MarketplaceOptions
{
    public static readonly IReadOnlyList<string> Listing = new[] { "eBay", "Facebook", "Mercari", "Other" };

    public static readonly IReadOnlyList<string> Sale = new[]
    {
        "eBay", "Facebook", "Mercari", "Cash", "Venmo", "Zelle", "CashApp", "PayPal", "Other"
    };
}
