namespace ResellerSystem.Modules.Inventory.Application;

/// <summary>Splits a total amount into N parts using the largest-remainder
/// method, guaranteeing Sum(result) == total exactly (to the cent) with no
/// floating-point drift — the first such helper in this codebase. Replaces
/// the naive `Math.Round(total/quantity, ...)` split
/// InventoryService.CreatePurchaseAsync used to do, which silently left a
/// shortfall whenever the division didn't come out even (e.g. $10/3 left a
/// $0.01 gap nobody reconciled).</summary>
public static class MoneyAllocator
{
    public static IReadOnlyList<decimal> AllocateExact(decimal total, IReadOnlyList<decimal> weights)
    {
        if (weights.Count == 0) return Array.Empty<decimal>();

        var roundedTotal = Math.Round(total, 2, MidpointRounding.AwayFromZero);
        if (weights.Count == 1) return new[] { roundedTotal };

        var totalWeight = weights.Sum();
        if (totalWeight <= 0)
        {
            // No meaningful weights to split by — fall back to an equal split by count.
            return AllocateExact(total, weights.Select(_ => 1m).ToArray());
        }

        var totalCents = roundedTotal * 100m;
        var floorCents = new long[weights.Count];
        var remainders = new decimal[weights.Count];
        long allocatedCents = 0;

        for (var i = 0; i < weights.Count; i++)
        {
            var rawCents = totalCents * weights[i] / totalWeight;
            var floored = Math.Floor(rawCents);
            floorCents[i] = (long)floored;
            remainders[i] = rawCents - floored;
            allocatedCents += floorCents[i];
        }

        // Guaranteed to be a non-negative integer strictly less than
        // weights.Count, since each remainder is in [0, 1).
        var leftoverCents = (long)(totalCents - allocatedCents);
        var byRemainderDesc = Enumerable.Range(0, weights.Count)
            .OrderByDescending(i => remainders[i])
            .ThenBy(i => i)
            .ToArray();

        for (var k = 0; k < leftoverCents; k++)
        {
            floorCents[byRemainderDesc[k]] += 1;
        }

        return floorCents.Select(cents => cents / 100m).ToArray();
    }
}
