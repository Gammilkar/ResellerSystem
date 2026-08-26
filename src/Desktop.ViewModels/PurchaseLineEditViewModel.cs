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

    // Descriptive fields collected in the Item Draft Editor — pure
    // pass-through, applied to every physical Item this line explodes into.
    [ObservableProperty] private string? _brand;
    [ObservableProperty] private string? _model;
    [ObservableProperty] private string? _serialNumber;
    [ObservableProperty] private string? _skuCustomLabel;
    [ObservableProperty] private string? _condition;
    [ObservableProperty] private string? _storageLocation;

    /// <summary>Files picked in the Item Draft Editor before this line's
    /// physical Item(s) exist — uploaded and linked to every Item this line
    /// creates only after the Purchase itself saves successfully (see
    /// PurchaseEditViewModel.SaveInternalAsync).</summary>
    public ObservableCollection<StagedDocumentRef> StagedDocuments { get; } = new();

    // Read-only, populated from the last preview/save response.
    [ObservableProperty] private decimal _linePurchaseCost;
    [ObservableProperty] private decimal _allocatedSalesTax;
    [ObservableProperty] private decimal _allocatedExpenses;
    [ObservableProperty] private decimal _finalLineCostBasis;

    /// <summary>The physical Items this line has created — clickable in the
    /// UI to open the full Item Card dialog. Empty for a not-yet-saved line.</summary>
    public ObservableCollection<PurchaseLineItemRefDto> CreatedItems { get; } = new();

    /// <summary>Raised when a money-affecting field changes, so the parent
    /// screen can debounce-recalculate the whole purchase's allocation live
    /// as the user types — see PurchaseEditViewModel.TriggerRecalculate.
    /// ItemName/CategoryName/Notes deliberately don't raise this since they
    /// don't affect the money math.</summary>
    public event Action? RecalculationNeeded;

    partial void OnQuantityChanged(int value) => RecalculationNeeded?.Invoke();
    partial void OnUnitPurchaseCostChanged(decimal value) => RecalculationNeeded?.Invoke();
    partial void OnManualAllocatedSalesTaxTextChanged(string? value) => RecalculationNeeded?.Invoke();
    partial void OnManualAllocatedExpensesTextChanged(string? value) => RecalculationNeeded?.Invoke();

    public PurchaseLineEditViewModel Clone() => new()
    {
        ItemName = ItemName,
        CategoryName = CategoryName,
        Quantity = Quantity,
        UnitPurchaseCost = UnitPurchaseCost,
        Notes = Notes,
        Brand = Brand,
        Model = Model,
        SerialNumber = SerialNumber,
        SkuCustomLabel = SkuCustomLabel,
        Condition = Condition,
        StorageLocation = StorageLocation
        // Id, allocation results, CreatedItems, and StagedDocuments
        // deliberately not copied — Duplicate Line makes a new,
        // not-yet-saved line with no runtime state of its own yet.
    };
}

/// <summary>A file picked for a not-yet-saved Item — just a local path
/// until the Purchase saves and the real Item exists to link it to.</summary>
public sealed record StagedDocumentRef(string FilePath, string DisplayName);
