using Npgsql;

namespace ResellerSystem.Modules.Sales.Data;

/// <summary>
/// Deliberate, documented exception to "modules don't reference each
/// other's schema": financial calculations (Net Proceeds/Net Profit/ROI)
/// need the Item's cost basis, which the Inventory module owns. Rather
/// than a C# project reference to Modules.Inventory (which would create a
/// hard compile-time coupling between two modules that should be able to
/// evolve independently) or duplicating cost-basis logic here, this does a
/// plain read-only SQL query against the `items` table — modules share one
/// physical tenant database, so this is a data-level read, not a service
/// dependency. It tolerates the table not existing yet (Inventory module
/// not installed) by returning null rather than throwing.
/// </summary>
public interface IItemCostBasisReader
{
    Task<decimal?> GetEffectiveCostBasisAsync(string connectionString, Guid itemId, CancellationToken ct = default);
}

public sealed class ItemCostBasisReader : IItemCostBasisReader
{
    public async Task<decimal?> GetEffectiveCostBasisAsync(string connectionString, Guid itemId, CancellationToken ct = default)
    {
        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(ct);

            await using var cmd = new NpgsqlCommand(
                "SELECT COALESCE(cost_basis_override, cost_basis_calculated) FROM items WHERE id = @id;", connection);
            cmd.Parameters.AddWithValue("id", itemId);

            var result = await cmd.ExecuteScalarAsync(ct);
            return result is decimal value ? value : null;
        }
        catch (PostgresException)
        {
            // items table doesn't exist (Inventory module not installed on
            // this server) — financials just won't include cost basis.
            return null;
        }
    }
}
