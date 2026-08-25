namespace ResellerSystem.Desktop.ViewModels;

/// <summary>Canonical Purchase.PurchaseType values going forward. The
/// column is free text server-side (no CHECK constraint), so older rows
/// may still hold the legacy "ResellerPermit"/"NoTax" values — kept
/// selectable here for the same reason as StatusOptions.</summary>
public static class PurchaseTypeOptions
{
    public sealed record Option(string Code, string Label);

    public static readonly IReadOnlyList<Option> All = new[]
    {
        new Option("TaxPaid", "TaxPaid"),
        new Option("TaxExempt", "TaxExempt"),
        new Option("Cash", "Cash"),
        new Option("ResellerPermit", "ResellerPermit (устар.)"),
        new Option("NoTax", "NoTax (устар.)")
    };
}
