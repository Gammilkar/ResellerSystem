namespace ResellerSystem.Desktop.ViewModels;

/// <summary>The Тип закупки dropdown's values.</summary>
public static class PurchaseTypeOptions
{
    public sealed record Option(string Code, string Label);

    public static readonly IReadOnlyList<Option> All = new[]
    {
        new Option("TaxPaid", "TaxPaid"),
        new Option("TaxExempt", "TaxExempt"),
        new Option("Cash", "Cash")
    };
}
