using Npgsql;
using ResellerSystem.Domain.Shared.Dto;
using ResellerSystem.Server.Application.Databases;
using ResellerSystem.Server.Data.Configuration;

namespace ResellerSystem.Modules.Reports.Application;

/// <summary>
/// Pure read-only cross-module reporting — same documented pattern as
/// Modules.Sales/Data/ItemCostBasisReader.cs (SQL reads against tables
/// owned by Inventory/Sales modules, no C# project dependency on those
/// modules). Every query tolerates a missing table (module not installed
/// on this server) by returning an empty result rather than throwing —
/// Reports should degrade gracefully, not break the whole app.
/// </summary>
public interface IReportsService
{
    Task<IReadOnlyList<MarketplaceProfitabilityRowDto>> GetMarketplaceProfitabilityAsync(CancellationToken ct = default);
    Task<IReadOnlyList<CategoryProfitabilityRowDto>> GetCategoryProfitabilityAsync(CancellationToken ct = default);
    Task<IReadOnlyList<InventoryAgingRowDto>> GetInventoryAgingAsync(CancellationToken ct = default);
    Task<FederalTaxSummaryDto> GetFederalTaxSummaryAsync(int year, CancellationToken ct = default);
    Task<IReadOnlyList<Form1099KSummaryDto>> Get1099KSummaryAsync(int year, CancellationToken ct = default);
}

public sealed class ReportsService : IReportsService
{
    private readonly ConnectionStringFactory _connectionStringFactory;
    private readonly ICurrentTenantAccessor _tenantAccessor;

    public ReportsService(ConnectionStringFactory connectionStringFactory, ICurrentTenantAccessor tenantAccessor)
    {
        _connectionStringFactory = connectionStringFactory;
        _tenantAccessor = tenantAccessor;
    }

    public async Task<IReadOnlyList<MarketplaceProfitabilityRowDto>> GetMarketplaceProfitabilityAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT
                s.marketplace,
                COUNT(*) AS sale_count,
                SUM(s.item_sale_price + s.buyer_paid_shipping + s.handling - s.seller_discount) AS gross_sales,
                COALESCE(fee_totals.total_fees, 0) AS total_fees,
                COALESCE(SUM(COALESCE(i.cost_basis_override, i.cost_basis_calculated)), 0) AS total_cost_basis
            FROM sales s
            LEFT JOIN items i ON i.id = s.item_id
            LEFT JOIN (
                SELECT sale_id, SUM(amount) AS total_fees FROM sale_fees GROUP BY sale_id
            ) fee_totals ON fee_totals.sale_id = s.id
            WHERE s.deleted_at IS NULL
            GROUP BY s.marketplace, fee_totals.total_fees
            ORDER BY gross_sales DESC;
            """;

        return await RunTolerantAsync(sql, reader => new MarketplaceProfitabilityRowDto
        {
            Marketplace = reader.GetString(0),
            SaleCount = (int)reader.GetInt64(1),
            GrossSales = reader.GetDecimal(2),
            TotalFees = reader.GetDecimal(3),
            TotalCostBasis = reader.GetDecimal(4),
            NetProfit = reader.GetDecimal(2) - reader.GetDecimal(3) - reader.GetDecimal(4),
            RoiPercent = reader.GetDecimal(4) > 0
                ? Math.Round((reader.GetDecimal(2) - reader.GetDecimal(3) - reader.GetDecimal(4)) / reader.GetDecimal(4) * 100, 2)
                : null
        }, ct);
    }

    public async Task<IReadOnlyList<CategoryProfitabilityRowDto>> GetCategoryProfitabilityAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT
                COALESCE(i.category_name, '(Uncategorized)') AS category,
                COUNT(*) AS purchased_count,
                COUNT(*) FILTER (WHERE s.id IS NOT NULL) AS sold_count,
                AVG(COALESCE(i.cost_basis_override, i.cost_basis_calculated)) AS avg_purchase_price,
                AVG(s.item_sale_price) AS avg_sale_price,
                COALESCE(SUM(s.item_sale_price), 0) - COALESCE(SUM(COALESCE(i.cost_basis_override, i.cost_basis_calculated)) FILTER (WHERE s.id IS NOT NULL), 0) AS net_profit,
                COALESCE(SUM(COALESCE(i.cost_basis_override, i.cost_basis_calculated)) FILTER (WHERE s.id IS NOT NULL), 0) AS cost_basis_sold
            FROM items i
            LEFT JOIN sales s ON s.item_id = i.id AND s.deleted_at IS NULL
            WHERE i.deleted_at IS NULL
            GROUP BY i.category_name
            ORDER BY net_profit DESC;
            """;

        return await RunTolerantAsync(sql, reader =>
        {
            var netProfit = reader.GetDecimal(5);
            var costBasisSold = reader.GetDecimal(6);
            return new CategoryProfitabilityRowDto
            {
                Category = reader.GetString(0),
                PurchasedCount = (int)reader.GetInt64(1),
                SoldCount = (int)reader.GetInt64(2),
                AveragePurchasePrice = reader.IsDBNull(3) ? 0 : reader.GetDecimal(3),
                AverageSalePrice = reader.IsDBNull(4) ? null : reader.GetDecimal(4),
                NetProfit = netProfit,
                RoiPercent = costBasisSold > 0 ? Math.Round(netProfit / costBasisSold * 100, 2) : null
            };
        }, ct);
    }

    public async Task<IReadOnlyList<InventoryAgingRowDto>> GetInventoryAgingAsync(CancellationToken ct = default)
    {
        // Architecture Plan v0.1 section 38 buckets: 0-30, 31-60, 61-90, 91-180, 180+.
        // Only items NOT sold (no matching row in sales) count as "aging inventory".
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

        return await RunTolerantAsync(sql, reader => new InventoryAgingRowDto
        {
            Bucket = reader.GetString(0),
            ItemCount = (int)reader.GetInt64(1),
            TotalCostBasis = reader.IsDBNull(2) ? 0 : reader.GetDecimal(2)
        }, ct);
    }

    public async Task<FederalTaxSummaryDto> GetFederalTaxSummaryAsync(int year, CancellationToken ct = default)
    {
        // Simplified Federal Business Tax Summary (Architecture Plan v0.1
        // section 33) — does NOT compute a final tax liability (the
        // architecture explicitly forbids that without a configured tax
        // profile), only the reconciliation inputs: gross sales,
        // marketplace fees, COGS (sold items' cost basis), net profit.
        // Refunds/discounts/other-expenses are NOT yet subtracted — see
        // KNOWN_LIMITATIONS.md "Reports/Tax module scope".
        const string sql = """
            SELECT
                COALESCE(SUM(s.item_sale_price + s.buyer_paid_shipping + s.handling - s.seller_discount), 0) AS gross_sales,
                COALESCE((SELECT SUM(f.amount) FROM sale_fees f
                          JOIN sales s2 ON s2.id = f.sale_id
                          WHERE EXTRACT(YEAR FROM s2.sale_date) = @year AND s2.deleted_at IS NULL), 0) AS marketplace_fees,
                COALESCE(SUM(COALESCE(i.cost_basis_override, i.cost_basis_calculated)), 0) AS cogs
            FROM sales s
            LEFT JOIN items i ON i.id = s.item_id
            WHERE EXTRACT(YEAR FROM s.sale_date) = @year AND s.deleted_at IS NULL;
            """;

        try
        {
            var tenant = _tenantAccessor.Require();
            var connectionString = _connectionStringFactory.BuildTenantConnectionString(tenant.PhysicalDatabaseName);

            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(ct);
            await using var cmd = new NpgsqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("year", year);
            await using var reader = await cmd.ExecuteReaderAsync(ct);

            if (!await reader.ReadAsync(ct))
            {
                return new FederalTaxSummaryDto { Year = year, GrossSales = 0, MarketplaceFees = 0, CostOfGoodsSold = 0, NetProfit = 0 };
            }

            var grossSales = reader.GetDecimal(0);
            var fees = reader.GetDecimal(1);
            var cogs = reader.GetDecimal(2);

            return new FederalTaxSummaryDto
            {
                Year = year,
                GrossSales = grossSales,
                MarketplaceFees = fees,
                CostOfGoodsSold = cogs,
                NetProfit = grossSales - fees - cogs
            };
        }
        catch (PostgresException)
        {
            return new FederalTaxSummaryDto { Year = year, GrossSales = 0, MarketplaceFees = 0, CostOfGoodsSold = 0, NetProfit = 0 };
        }
    }

    public async Task<IReadOnlyList<Form1099KSummaryDto>> Get1099KSummaryAsync(int year, CancellationToken ct = default)
    {
        // INTERNAL RECONCILIATION AID ONLY — not an official IRS/marketplace
        // document (Architecture Plan v0.1 section 28-29). Box 1a here is
        // approximated as GrossTransactionAmount + MarketplaceCollectedTax
        // (i.e. everything the buyer paid, which is what 1099-K Box 1a
        // represents) — never reduced by fees/refunds/cost basis.
        const string sql = """
            SELECT
                s.marketplace,
                SUM(s.item_sale_price + s.buyer_paid_shipping + s.handling - s.seller_discount + s.marketplace_collected_tax) AS box1a,
                COUNT(*) AS transaction_count
            FROM sales s
            WHERE EXTRACT(YEAR FROM s.sale_date) = @year AND s.deleted_at IS NULL
            GROUP BY s.marketplace
            ORDER BY box1a DESC;
            """;

        try
        {
            var tenant = _tenantAccessor.Require();
            var connectionString = _connectionStringFactory.BuildTenantConnectionString(tenant.PhysicalDatabaseName);

            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(ct);
            await using var cmd = new NpgsqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("year", year);
            await using var reader = await cmd.ExecuteReaderAsync(ct);

            var results = new List<Form1099KSummaryDto>();
            while (await reader.ReadAsync(ct))
            {
                results.Add(new Form1099KSummaryDto
                {
                    Year = year,
                    Marketplace = reader.GetString(0),
                    Box1aGrossPaymentAmount = reader.GetDecimal(1),
                    Box3TransactionCount = (int)reader.GetInt64(2)
                });
            }
            return results;
        }
        catch (PostgresException)
        {
            return Array.Empty<Form1099KSummaryDto>();
        }
    }

    private async Task<IReadOnlyList<T>> RunTolerantAsync<T>(string sql, Func<NpgsqlDataReader, T> map, CancellationToken ct)
    {
        try
        {
            var tenant = _tenantAccessor.Require();
            var connectionString = _connectionStringFactory.BuildTenantConnectionString(tenant.PhysicalDatabaseName);

            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(ct);
            await using var cmd = new NpgsqlCommand(sql, connection);
            await using var reader = await cmd.ExecuteReaderAsync(ct);

            var results = new List<T>();
            while (await reader.ReadAsync(ct))
            {
                results.Add(map(reader));
            }
            return results;
        }
        catch (PostgresException)
        {
            // Required tables (items/sales/sale_fees) don't exist yet —
            // Inventory/Sales modules not installed on this server.
            return Array.Empty<T>();
        }
    }
}
