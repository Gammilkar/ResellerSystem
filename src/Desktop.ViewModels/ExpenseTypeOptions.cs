namespace ResellerSystem.Desktop.ViewModels;

/// <summary>Client-side mirror of Modules.Expenses.Domain.ExpenseTypes —
/// the desktop client can't reference server module projects.</summary>
public static class ExpenseTypeOptions
{
    public static readonly IReadOnlyList<string> All = new[]
    {
        "ShippingLabel", "ReturnShipping", "Packaging", "Supplies", "Software", "Storage", "Mileage", "Other"
    };
}
