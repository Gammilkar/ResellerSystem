using Npgsql;
using ResellerSystem.Domain.Shared.Dto;
using ResellerSystem.Server.Application.Databases;
using ResellerSystem.Server.Data.Configuration;

namespace ResellerSystem.Modules.Dashboard.Application;

/// <summary>
/// Product Specification section 22 ("Dashboard"). Pure read-only
/// cross-module aggregation — same documented pattern as
/// Modules.Reports/Application/ReportsService.cs (SQL reads against tables
/// owned by Inventory/Sales, no C# project dependency on those modules).
/// Every query tolerates a missing table (module not installed on this
/// server) by returning zeroes/empty rather than throwing.
/// </summary>
public interface IDashboardService
{
    Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken ct = default);
}

public sealed class DashboardService : IDashboardService
{
    private readonly ConnectionStringFactory _connectionStringFactory;
    private readonly ICurrentTenantAccessor _tenantAccessor;

    public DashboardService(ConnectionStringFactory connectionStringFactory, ICurrentTenantAccessor tenantAccessor)
    {
        _connectionStringFactory = connectionStringFactory;
        _tenantAccessor = tenantAccessor;
    }

    public async Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken ct = default)
    {
        var (inventoryCostBasis, inventoryCount) = await GetInventoryOnHandAsync(ct);
        var sales = await GetSalesAggregatesAsync(ct);
        var aging = await GetInventoryAgingAsync(ct);

        return new DashboardSummaryDto
        {
            InventoryOnHandCostBasis = inventoryCostBasis,
            InventoryOnHandCount = inventoryCount,
            NetProfitAllTime = sales.ProfitAllTime,
            NetProfitThisMonth = sales.ProfitThisMonth,
            NetProfitThisWeek = sales.ProfitThisWeek,
            ItemsSoldAllTime = sales.SoldAllTime,
            ItemsSoldThisMonth = sales.SoldThisMonth,
            ItemsSoldThisWeek = sales.SoldThisWeek,
            GrossSalesAllTime = sales.GrossAllTime,
            AverageRoiPercent = sales.AverageRoiPercent,
            AverageDaysToSell = sales.AverageDaysToSell,
            InventoryAging = aging
        };
    }

    private async Task<(decimal CostBasis, int Count)> GetInventoryOnHandAsync(CancellationToken ct)
    {
        // Same "on hand" definition as ReportsService.GetInventoryAgingAsync:
        // not soft-deleted and no matching non-deleted sale row.
        const string sql = """
            SELECT
                COUNT(*) AS item_count,
                COALESCE(SUM(COALESCE(i.cost_basis_override, i.cost_basis_calculated)), 0) AS total_cost_basis
            FROM items i
            WHERE i.deleted_at IS NULL
              AND NOT EXISTS (SELECT 1 FROM sales s WHERE s.item_id = i.id AND s.deleted_at IS NULL);
            """;

        try
        {
            var tenant = _tenantAccessor.Require();
            var connectionString = _connectionStringFactory.BuildTenantConnectionString(tenant.PhysicalDatabaseName);

            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(ct);
            await using var cmd = new NpgsqlCommand(sql, connection);
            await using var reader = await cmd.ExecuteReaderAsync(ct);

            if (!await reader.ReadAsync(ct)) return (0, 0);

            var count = (int)reader.GetInt64(0);
            var costBasis = reader.IsDBNull(1) ? 0 : reader.GetDecimal(1);
            return (costBasis, count);
        }
        catch (PostgresException)
        {
            return (0, 0);
        }
    }

    private sealed record SalesAggregates(
        int SoldAllTime, int SoldThisMonth, int SoldThisWeek,
        decimal ProfitAllTime, decimal ProfitThisMonth, decimal ProfitThisWeek,
        decimal GrossAllTime, decimal? AverageRoiPercent, double? AverageDaysToSell);

    private async Task<SalesAggregates> GetSalesAggregatesAsync(CancellationToken ct)
    {
        // Gross/profit formulas mirror ReportsService.GetMarketplaceProfitabilityAsync
        // for consistency across Dashboard and Reports.
        const string sql = """
            WITH fee_totals AS (
                SELECT sale_id, SUM(amount) AS total_fees FROM sale_fees GROUP BY sale_id
            ),
            sale_calc AS (
                SELECT
                    s.sale_date,
                    (s.item_sale_price + s.buyer_paid_shipping + s.handling - s.seller_discount) AS gross,
                    COALESCE(ft.total_fees, 0) AS fees,
                    COALESCE(i.cost_basis_override, i.cost_basis_calculated, 0) AS cost_basis,
                    p.purchase_date
                FROM sales s
                LEFT JOIN items i ON i.id = s.item_id
                LEFT JOIN purchases p ON p.id = i.purchase_id
                LEFT JOIN fee_totals ft ON ft.sale_id = s.id
                WHERE s.deleted_at IS NULL
            )
            SELECT
                COUNT(*) AS sold_all_time,
                COUNT(*) FILTER (WHERE sale_date >= date_trunc('month', now())) AS sold_month,
                COUNT(*) FILTER (WHERE sale_date >= date_trunc('week', now())) AS sold_week,
                COALESCE(SUM(gross - fees - cost_basis), 0) AS profit_all_time,
                COALESCE(SUM(gross - fees - cost_basis) FILTER (WHERE sale_date >= date_trunc('month', now())), 0) AS profit_month,
                COALESCE(SUM(gross - fees - cost_basis) FILTER (WHERE sale_date >= date_trunc('week', now())), 0) AS profit_week,
                COALESCE(SUM(gross), 0) AS gross_all_time,
                AVG(sale_date - purchase_date) FILTER (WHERE purchase_date IS NOT NULL) AS avg_days_to_sell,
                SUM(cost_basis) AS total_cost_basis_sold
            FROM sale_calc;
            """;

        try
        {
            var tenant = _tenantAccessor.Require();
            var connectionString = _connectionStringFactory.BuildTenantConnectionString(tenant.PhysicalDatabaseName);

            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(ct);
            await using var cmd = new NpgsqlCommand(sql, connection);
            await using var reader = await cmd.ExecuteReaderAsync(ct);

            if (!await reader.ReadAsync(ct))
            {
                return new SalesAggregates(0, 0, 0, 0, 0, 0, 0, null, null);
            }

            var soldAllTime = (int)reader.GetInt64(0);
            var soldMonth = (int)reader.GetInt64(1);
            var soldWeek = (int)reader.GetInt64(2);
            var profitAllTime = reader.GetDecimal(3);
            var profitMonth = reader.GetDecimal(4);
            var profitWeek = reader.GetDecimal(5);
            var grossAllTime = reader.GetDecimal(6);
            var avgDaysToSell = reader.IsDBNull(7) ? (double?)null : reader.GetDouble(7);
            var totalCostBasisSold = reader.IsDBNull(8) ? 0 : reader.GetDecimal(8);

            decimal? avgRoi = totalCostBasisSold > 0
                ? Math.Round(profitAllTime / totalCostBasisSold * 100, 2)
                : null;

            return new SalesAggregates(soldAllTime, soldMonth, soldWeek, profitAllTime, profitMonth, profitWeek, grossAllTime, avgRoi, avgDaysToSell);
        }
        catch (PostgresException)
        {
            return new SalesAggregates(0, 0, 0, 0, 0, 0, 0, null, null);
        }
    }

    private async Task<IReadOnlyList<InventoryAgingRowDto>> GetInventoryAgingAsync(CancellationToken ct)
    {
        // Product Specification section 65 buckets: 0-30, 31-60, 61-90, 91-180, 180+.
        // Same query as ReportsService.GetInventoryAgingAsync (Modules.Dashboard
        // and Modules.Reports don't reference each other — see ReportsService
        // doc comment on the "no cross-module project reference" convention).
        const string sql = """
            SELECT
                CASE
                    WHEN age_days <= 30 THEN '0-30'
                    WHEN age_days <= 60 THEN '31-60'
                    WHEN age_days <= 90 THEN '61-90'
                    WHEN age_days <= 180 THEN '91-180'
                    ELSE '180+'
                END AS bucket,
                COUNT(*) AS item_count,
                SUM(COALESCE(cost_basis_override, cost_basis_calculated)) AS total_cost_basis
            FROM (
                SELECT
                    i.cost_basis_override,
                    i.cost_basis_calculated,
                    EXTRACT(DAY FROM now() - i.created_at)::int AS age_days
                FROM items i
                WHERE i.deleted_at IS NULL
                  AND NOT EXISTS (SELECT 1 FROM sales s WHERE s.item_id = i.id AND s.deleted_at IS NULL)
            ) aged
            GROUP BY bucket
            ORDER BY MIN(age_days);
            """;

        try
        {
            var tenant = _tenantAccessor.Require();
            var connectionString = _connectionStringFactory.BuildTenantConnectionString(tenant.PhysicalDatabaseName);

            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(ct);
            await using var cmd = new NpgsqlCommand(sql, connection);
            await using var reader = await cmd.ExecuteReaderAsync(ct);

            var results = new List<InventoryAgingRowDto>();
            while (await reader.ReadAsync(ct))
            {
                results.Add(new InventoryAgingRowDto
                {
                    Bucket = reader.GetString(0),
                    ItemCount = (int)reader.GetInt64(1),
                    TotalCostBasis = reader.IsDBNull(2) ? 0 : reader.GetDecimal(2)
                });
            }
            return results;
        }
        catch (PostgresException)
        {
            return Array.Empty<InventoryAgingRowDto>();
        }
    }
}
