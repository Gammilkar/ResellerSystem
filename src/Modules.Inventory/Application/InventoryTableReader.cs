using Npgsql;
using ResellerSystem.Domain.Shared.Dto;
using ResellerSystem.Server.Application.Databases;
using ResellerSystem.Server.Data.Configuration;

namespace ResellerSystem.Modules.Inventory.Application;

/// <summary>
/// Product Specification section 34 ("Таблица товаров") — the main
/// Excel-like Inventory grid. Same documented pattern as
/// Modules.Reports/Application/ReportsService.cs and
/// Modules.Sales/Data/ItemCostBasisReader.cs: raw SQL reads against
/// tables owned by Sales (listings/sales), no C# project dependency on
/// that module. Tolerates Sales not being installed by falling back to
/// Purchase/Item-only rows.
/// </summary>
public interface IInventoryTableReader
{
    Task<IReadOnlyList<InventoryTableRowDto>> GetTableAsync(CancellationToken ct = default);
}

public sealed class InventoryTableReader : IInventoryTableReader
{
    private readonly ConnectionStringFactory _connectionStringFactory;
    private readonly ICurrentTenantAccessor _tenantAccessor;

    public InventoryTableReader(ConnectionStringFactory connectionStringFactory, ICurrentTenantAccessor tenantAccessor)
    {
        _connectionStringFactory = connectionStringFactory;
        _tenantAccessor = tenantAccessor;
    }

    public async Task<IReadOnlyList<InventoryTableRowDto>> GetTableAsync(CancellationToken ct = default)
    {
        const string sqlWithSales = """
            SELECT
                i.id, i.item_number, i.name, i.status,
                p.id AS purchase_id, p.purchase_date, p.source_name, p.purchase_type,
                COALESCE(i.cost_basis_override, i.cost_basis_calculated) AS cost_basis,
                l.id AS listing_id, l.published_date, l.marketplace AS listing_marketplace,
                s.id AS sale_id, s.sale_date, s.marketplace AS sale_marketplace, s.item_sale_price
            FROM items i
            JOIN purchases p ON p.id = i.purchase_id
            LEFT JOIN LATERAL (
                SELECT id, published_date, marketplace
                FROM listings
                WHERE listings.item_id = i.id AND listings.deleted_at IS NULL
                ORDER BY published_date DESC NULLS LAST, created_at DESC
                LIMIT 1
            ) l ON true
            LEFT JOIN LATERAL (
                SELECT id, sale_date, marketplace, item_sale_price
                FROM sales
                WHERE sales.item_id = i.id AND sales.deleted_at IS NULL
                ORDER BY sale_date DESC
                LIMIT 1
            ) s ON true
            WHERE i.deleted_at IS NULL
            ORDER BY p.purchase_date DESC, i.item_number DESC;
            """;

        const string sqlItemsOnly = """
            SELECT
                i.id, i.item_number, i.name, i.status,
                p.id AS purchase_id, p.purchase_date, p.source_name, p.purchase_type,
                COALESCE(i.cost_basis_override, i.cost_basis_calculated) AS cost_basis
            FROM items i
            JOIN purchases p ON p.id = i.purchase_id
            WHERE i.deleted_at IS NULL
            ORDER BY p.purchase_date DESC, i.item_number DESC;
            """;

        var tenant = _tenantAccessor.Require();
        var connectionString = _connectionStringFactory.BuildTenantConnectionString(tenant.PhysicalDatabaseName);

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);

        try
        {
            return await ReadAsync(connection, sqlWithSales, includeSalesColumns: true, ct);
        }
        catch (PostgresException)
        {
            // listings/sales tables don't exist — Sales module not
            // installed on this server. Degrade to Purchase/Item only
            // rather than failing the whole screen.
            return await ReadAsync(connection, sqlItemsOnly, includeSalesColumns: false, ct);
        }
    }

    private static async Task<IReadOnlyList<InventoryTableRowDto>> ReadAsync(
        NpgsqlConnection connection, string sql, bool includeSalesColumns, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(sql, connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var results = new List<InventoryTableRowDto>();
        while (await reader.ReadAsync(ct))
        {
            // Column layout: 0=id 1=item_number 2=name 3=status
            // 4=purchase_id 5=purchase_date 6=source_name 7=purchase_type
            // 8=cost_basis — identical in both queries. Only sqlWithSales
            // has more: 9=listing_id 10=published_date 11=listing_marketplace
            // 12=sale_id 13=sale_date 14=sale_marketplace 15=item_sale_price.
            var purchaseDate = DateOnly.FromDateTime(reader.GetDateTime(5));
            Guid? listingId = includeSalesColumns && !reader.IsDBNull(9) ? reader.GetGuid(9) : null;
            DateOnly? publishedDate = includeSalesColumns && !reader.IsDBNull(10) ? DateOnly.FromDateTime(reader.GetDateTime(10)) : null;
            string? listingMarketplace = includeSalesColumns && !reader.IsDBNull(11) ? reader.GetString(11) : null;
            Guid? saleId = includeSalesColumns && !reader.IsDBNull(12) ? reader.GetGuid(12) : null;
            DateOnly? saleDate = includeSalesColumns && !reader.IsDBNull(13) ? DateOnly.FromDateTime(reader.GetDateTime(13)) : null;
            string? saleMarketplace = includeSalesColumns && !reader.IsDBNull(14) ? reader.GetString(14) : null;
            decimal? salePrice = includeSalesColumns && !reader.IsDBNull(15) ? reader.GetDecimal(15) : null;

            int? daysListed = null;
            var listedFrom = publishedDate ?? purchaseDate;
            if (saleDate is not null)
            {
                daysListed = saleDate.Value.DayNumber - listedFrom.DayNumber;
            }
            else if (publishedDate is not null)
            {
                daysListed = DateOnly.FromDateTime(DateTime.UtcNow).DayNumber - listedFrom.DayNumber;
            }

            results.Add(new InventoryTableRowDto
            {
                ItemId = reader.GetGuid(0),
                ItemNumber = reader.GetInt64(1),
                Name = reader.GetString(2),
                Status = reader.GetString(3),
                PurchaseId = reader.GetGuid(4),
                PurchaseDate = purchaseDate,
                PurchaseSourceName = reader.GetString(6),
                PurchaseType = reader.GetString(7),
                CostBasis = reader.GetDecimal(8),
                ListingId = listingId,
                ListingPublishedDate = publishedDate,
                ListingMarketplace = listingMarketplace,
                SaleId = saleId,
                SaleDate = saleDate,
                SaleMarketplace = saleMarketplace,
                SalePrice = salePrice,
                DaysListed = daysListed
            });
        }
        return results;
    }
}
