namespace ResellerSystem.Desktop.ViewModels;

/// <summary>The 4 statuses the Inventory grid's Статус dropdown offers.</summary>
public static class StatusOptions
{
    public sealed record Option(string Code, string Label);

    public static readonly IReadOnlyList<Option> All = new[]
    {
        new Option("InStock", "на складе"),
        new Option("Listed", "опубликован"),
        new Option("Sold", "продан"),
        new Option("Returned", "возврат")
    };
}
