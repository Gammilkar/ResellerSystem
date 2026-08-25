namespace ResellerSystem.Domain.Shared.Dto;

/// <summary>Product Specification section 22 ("Dashboard"). All figures are
/// computed live on every request — nothing here is cached or stored.</summary>
public sealed class DashboardSummaryDto
{
    // Inventory
    public required decimal InventoryOnHandCostBasis { get; init; }
    public required int InventoryOnHandCount { get; init; }

    // Profit
    public required decimal NetProfitAllTime { get; init; }
    public required decimal NetProfitThisMonth { get; init; }
    public required decimal NetProfitThisWeek { get; init; }

    // Sales
    public required int ItemsSoldAllTime { get; init; }
    public required int ItemsSoldThisMonth { get; init; }
    public required int ItemsSoldThisWeek { get; init; }

    // Additional metrics
    public required decimal GrossSalesAllTime { get; init; }
    public decimal? AverageRoiPercent { get; init; }
    public double? AverageDaysToSell { get; init; }

    public required IReadOnlyList<InventoryAgingRowDto> InventoryAging { get; init; }
}
