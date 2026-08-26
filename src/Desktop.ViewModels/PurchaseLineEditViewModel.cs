using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ResellerSystem.Domain.Shared.Dto;

namespace ResellerSystem.Desktop.ViewModels;

/// <summary>One editable row in the New/Edit Purchase screen's Items grid —
/// unlike the read-only Inventory grid, this binds two-way directly since
/// it's a genuinely mutable ViewModel, not an immutable server DTO. Id is
/// null for a line the user just added (not yet saved); set once it
/// round-trips through Create/UpdateAsync.</summary>
public sealed partial class PurchaseLineEditViewModel : ObservableObject
{
    public Guid? Id { get; set; }

    [ObservableProperty] private string _itemName = string.Empty;
    [ObservableProperty] private string? _categoryName;
    [ObservableProperty] private int _quantity = 1;
    [ObservableProperty] private decimal _unitPurchaseCost;
    [ObservableProperty] private string? _notes;
    [ObservableProperty] private string? _manualAllocatedSalesTaxText;
    [ObservableProperty] private string? _manualAllocatedExpensesText;

    // Read-only, populated from the last preview/save response.
    [ObservableProperty] private decimal _linePurchaseCost;
    [ObservableProperty] private decimal _allocatedSalesTax;
    [ObservableProperty] private decimal _allocatedExpenses;
    [ObservableProperty] private decimal _finalLineCostBasis;

    /// <summary>The physical Items this line has created — clickable in the
    /// UI to open the full Item Card dialog. Empty for a not-yet-saved line.</summary>
    public ObservableCollection<PurchaseLineItemRefDto> CreatedItems { get; } = new();

    public PurchaseLineEditViewModel Clone() => new()
    {
        ItemName = ItemName,
        CategoryName = CategoryName,
        Quantity = Quantity,
        UnitPurchaseCost = UnitPurchaseCost,
        Notes = Notes
        // Id, allocation results, and CreatedItems deliberately not copied —
        // Duplicate Line makes a new, not-yet-saved line.
    };
}
