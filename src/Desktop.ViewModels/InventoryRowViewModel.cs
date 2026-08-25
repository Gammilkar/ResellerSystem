using CommunityToolkit.Mvvm.ComponentModel;
using ResellerSystem.Domain.Shared.Dto;

namespace ResellerSystem.Desktop.ViewModels;

/// <summary>
/// Wraps one InventoryTableRowDto (an immutable server DTO) with a
/// mutable, per-row Height — Avalonia's DataGrid has no drag handle for
/// resizing an individual row (verified against the assembly: only
/// column-resize infrastructure exists), so this is the actual
/// per-row-adjustable mechanism: DataGridRow's Style binds Height to this
/// wrapper's own RowHeight (see InventoryView.axaml), and a dedicated
/// "Row Height" column lets the user type a value per row, the same way
/// the DataGrid already lets them type into any other cell.
/// </summary>
public sealed partial class InventoryRowViewModel : ObservableObject
{
    public InventoryTableRowDto Dto { get; }

    public InventoryRowViewModel(InventoryTableRowDto dto, double initialRowHeight)
    {
        Dto = dto;
        _rowHeight = initialRowHeight;
    }

    [ObservableProperty]
    private double _rowHeight;

    public Guid ItemId => Dto.ItemId;
    public long ItemNumber => Dto.ItemNumber;
    public string Name => Dto.Name;
    public string Status => Dto.Status;
    public DateOnly PurchaseDate => Dto.PurchaseDate;
    public string PurchaseSourceName => Dto.PurchaseSourceName;
    public string PurchaseType => Dto.PurchaseType;
    public decimal CostBasis => Dto.CostBasis;
    public DateOnly? ListingPublishedDate => Dto.ListingPublishedDate;
    public string? ListingMarketplace => Dto.ListingMarketplace;
    public DateOnly? SaleDate => Dto.SaleDate;
    public string? SaleMarketplace => Dto.SaleMarketplace;
    public decimal? SalePrice => Dto.SalePrice;
    public int? DaysListed => Dto.DaysListed;
}
