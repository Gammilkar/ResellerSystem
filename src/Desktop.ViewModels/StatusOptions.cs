namespace ResellerSystem.Desktop.ViewModels;

/// <summary>Client-side mirror of the status codes Modules.Inventory.Domain
/// .ItemStatuses defines server-side — the desktop client can't reference
/// server module projects, so the codes are duplicated here. Only the 4
/// requested values are meant for everyday use; the rest are legacy codes
/// kept selectable so a row that already holds one is never silently
/// misrepresented by a dropdown that doesn't include its own value.</summary>
public static class StatusOptions
{
    public sealed record Option(string Code, string Label);

    public static readonly IReadOnlyList<Option> All = new[]
    {
        new Option("InStock", "на складе"),
        new Option("Listed", "опубликован"),
        new Option("Sold", "продан"),
        new Option("Returned", "возврат"),
        new Option("Purchased", "Purchased (устар.)"),
        new Option("NotListed", "NotListed (устар.)"),
        new Option("Relisted", "Relisted (устар.)"),
        new Option("WrittenOff", "WrittenOff (устар.)"),
        new Option("Lost", "Lost (устар.)"),
        new Option("PersonalUse", "PersonalUse (устар.)")
    };
}
