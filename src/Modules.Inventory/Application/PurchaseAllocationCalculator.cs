using ResellerSystem.Domain.Shared.Dto;

namespace ResellerSystem.Modules.Inventory.Application;

/// <summary>Pure calculation, no DB access — shared by the read-only
/// preview-allocation endpoint and the real Create/Update path, so the
/// client and server can never compute different numbers (Product
/// Specification §39-40: business logic lives once, server-side).</summary>
public static class PurchaseAllocationCalculator
{
    public static PurchaseAllocationResult Calculate(
        IReadOnlyList<PurchaseItemLineInput> itemLines,
        IReadOnlyList<PurchaseExpenseLineInput> expenseLines,
        decimal? taxableAmountInput,
        decimal? salesTaxRate,
        decimal? salesTaxAmountOverride,
        string salesTaxAllocationMethod,
        string expenseAllocationMethod,
        decimal manualAdjustment)
    {
        var errors = new List<string>();
        if (itemLines.Count == 0) errors.Add("Добавьте хотя бы один товар.");
        foreach (var line in itemLines)
        {
            if (line.Quantity < 1) errors.Add($"Строка «{line.ItemName}»: количество должно быть не меньше 1.");
        }

        var lineCosts = itemLines.Select(l => l.Quantity * l.UnitPurchaseCost).ToArray();
        var merchandiseSubtotal = lineCosts.Sum();
        var taxableAmount = taxableAmountInput ?? merchandiseSubtotal;

        var salesTaxCalculated = salesTaxRate is { } rate
            ? Math.Round(taxableAmount * rate / 100m, 2, MidpointRounding.AwayFromZero)
            : 0m;
        var salesTaxAmount = salesTaxAmountOverride ?? salesTaxCalculated;

        var totalExpenses = expenseLines.Sum(e => e.Amount);
        var totalPurchaseCost = merchandiseSubtotal + salesTaxAmount + totalExpenses + manualAdjustment;

        var allocatedTax = AllocatePool(salesTaxAmount, salesTaxAllocationMethod, itemLines, lineCosts,
            l => l.ManualAllocatedSalesTax, errors, "Sales Tax");

        // ManualAdjustment isn't its own separate pool for allocation
        // purposes — it's folded into the expense pool so item cost bases
        // reflect the true final cost and AllocatedTotal reconciles to
        // TotalPurchaseCost exactly outside Manual mode. TotalExpenses
        // still reports Σ(ExpenseLines) alone for display, matching the
        // Product Specification's formula listing them as separate lines.
        var allocatedExpenses = AllocatePool(totalExpenses + manualAdjustment, expenseAllocationMethod, itemLines, lineCosts,
            l => l.ManualAllocatedExpenses, errors, "Expenses/Adjustment");

        var lineResults = new List<PurchaseAllocationLineResultDto>();
        decimal allocatedTotal = 0;
        var physicalItemsToCreate = 0;

        for (var i = 0; i < itemLines.Count; i++)
        {
            var line = itemLines[i];
            var lineCost = lineCosts[i];
            var finalLineCostBasis = lineCost + allocatedTax[i] + allocatedExpenses[i];
            allocatedTotal += finalLineCostBasis;

            var quantity = Math.Max(line.Quantity, 0);
            physicalItemsToCreate += quantity;
            var unitCostBases = quantity > 0
                ? MoneyAllocator.AllocateExact(finalLineCostBasis, Enumerable.Repeat(1m, quantity).ToArray())
                : Array.Empty<decimal>();

            lineResults.Add(new PurchaseAllocationLineResultDto
            {
                Id = line.Id,
                LineNumber = i + 1,
                ItemName = line.ItemName,
                Quantity = line.Quantity,
                UnitPurchaseCost = line.UnitPurchaseCost,
                LinePurchaseCost = lineCost,
                AllocatedSalesTax = allocatedTax[i],
                AllocatedExpenses = allocatedExpenses[i],
                FinalLineCostBasis = finalLineCostBasis,
                UnitCostBases = unitCostBases
            });
        }

        var difference = Math.Round(totalPurchaseCost - allocatedTotal, 2, MidpointRounding.AwayFromZero);
        if (difference != 0)
        {
            errors.Add($"Распределение не совпадает с итогом закупки: разница {difference:F2}.");
        }

        return new PurchaseAllocationResult
        {
            MerchandiseSubtotal = merchandiseSubtotal,
            TaxableAmount = taxableAmount,
            SalesTaxAmount = salesTaxAmount,
            TotalExpenses = totalExpenses,
            ManualAdjustment = manualAdjustment,
            TotalPurchaseCost = totalPurchaseCost,
            AllocatedTotal = allocatedTotal,
            Difference = difference,
            PhysicalItemsToCreate = physicalItemsToCreate,
            IsReadyToSave = difference == 0 && errors.Count == 0,
            Lines = lineResults,
            ValidationErrors = errors
        };
    }

    /// <summary>"Proportional" (weight = line cost) | "EqualPerUnit"
    /// (weight = Quantity) | "Manual" (each line's own manual value is
    /// authoritative — the allocator is skipped entirely; a mismatch
    /// against the pool total surfaces as a validation error, which is
    /// exactly the Strict/Allow-Difference case the caller checks for).</summary>
    private static IReadOnlyList<decimal> AllocatePool(
        decimal poolAmount, string method, IReadOnlyList<PurchaseItemLineInput> lines, decimal[] lineCosts,
        Func<PurchaseItemLineInput, decimal?> manualSelector, List<string> errors, string poolName)
    {
        if (lines.Count == 0) return Array.Empty<decimal>();

        if (method == "Manual")
        {
            var manualValues = lines.Select(l => manualSelector(l) ?? 0m).ToArray();
            var manualSum = manualValues.Sum();
            if (Math.Round(manualSum - poolAmount, 2, MidpointRounding.AwayFromZero) != 0)
            {
                errors.Add($"{poolName}: ручное распределение ({manualSum:F2}) не совпадает с суммой ({poolAmount:F2}).");
            }
            return manualValues;
        }

        var weights = method == "EqualPerUnit"
            ? lines.Select(l => (decimal)Math.Max(l.Quantity, 0)).ToArray()
            : lineCosts; // "Proportional" (default)

        return MoneyAllocator.AllocateExact(poolAmount, weights);
    }
}
