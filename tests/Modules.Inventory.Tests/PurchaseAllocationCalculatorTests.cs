using FluentAssertions;
using ResellerSystem.Domain.Shared.Dto;
using ResellerSystem.Modules.Inventory.Application;
using Xunit;

namespace ResellerSystem.Modules.Inventory.Tests;

public class PurchaseAllocationCalculatorTests
{
    private static PurchaseItemLineInput Line(string name, int quantity, decimal unitCost,
        decimal? manualTax = null, decimal? manualExpenses = null) => new()
    {
        ItemName = name,
        Quantity = quantity,
        UnitPurchaseCost = unitCost,
        ManualAllocatedSalesTax = manualTax,
        ManualAllocatedExpenses = manualExpenses
    };

    [Fact]
    public void Calculate_reproduces_the_worked_example_from_the_product_spec()
    {
        // Purchase #315: Subtotal $300, Sales Tax $31.20 (10.4%), Buyer
        // Premium $54, Other Expenses $10 -> Total $395.20.
        var result = PurchaseAllocationCalculator.Calculate(
            itemLines: new[] { Line("Sony Camera", 1, 300m) },
            expenseLines: new[]
            {
                new PurchaseExpenseLineInput { ExpenseType = "BuyerPremium", Amount = 54m },
                new PurchaseExpenseLineInput { ExpenseType = "Other", Amount = 10m }
            },
            taxableAmountInput: null,
            salesTaxRate: 10.4m,
            salesTaxAmountOverride: null,
            salesTaxAllocationMethod: "Proportional",
            expenseAllocationMethod: "Proportional",
            manualAdjustment: 0m);

        result.MerchandiseSubtotal.Should().Be(300m);
        result.SalesTaxAmount.Should().Be(31.20m);
        result.TotalExpenses.Should().Be(64m);
        result.TotalPurchaseCost.Should().Be(395.20m);
        result.AllocatedTotal.Should().Be(395.20m);
        result.Difference.Should().Be(0m);
        result.IsReadyToSave.Should().BeTrue();
        result.PhysicalItemsToCreate.Should().Be(1);
    }

    [Fact]
    public void Calculate_proportional_expense_allocation_matches_the_camera_lens_example()
    {
        // Buyer Premium $50 across Camera $100 / Lens $300 -> $12.50 / $37.50.
        var result = PurchaseAllocationCalculator.Calculate(
            itemLines: new[] { Line("Camera", 1, 100m), Line("Lens", 1, 300m) },
            expenseLines: new[] { new PurchaseExpenseLineInput { ExpenseType = "BuyerPremium", Amount = 50m } },
            taxableAmountInput: null,
            salesTaxRate: null,
            salesTaxAmountOverride: null,
            salesTaxAllocationMethod: "Proportional",
            expenseAllocationMethod: "Proportional",
            manualAdjustment: 0m);

        result.Lines[0].AllocatedExpenses.Should().Be(12.50m);
        result.Lines[1].AllocatedExpenses.Should().Be(37.50m);
        result.Difference.Should().Be(0m);
    }

    [Fact]
    public void Calculate_equal_per_unit_weights_by_quantity_not_by_line()
    {
        // 1 unit vs 3 units sharing $40 equally *per unit* -> $10 / $30, not $20/$20.
        var result = PurchaseAllocationCalculator.Calculate(
            itemLines: new[] { Line("Single", 1, 10m), Line("Triple", 3, 10m) },
            expenseLines: new[] { new PurchaseExpenseLineInput { ExpenseType = "Delivery", Amount = 40m } },
            taxableAmountInput: null,
            salesTaxRate: null,
            salesTaxAmountOverride: null,
            salesTaxAllocationMethod: "Proportional",
            expenseAllocationMethod: "EqualPerUnit",
            manualAdjustment: 0m);

        result.Lines[0].AllocatedExpenses.Should().Be(10m);
        result.Lines[1].AllocatedExpenses.Should().Be(30m);
    }

    [Fact]
    public void Calculate_manual_allocation_that_sums_correctly_is_ready_to_save()
    {
        var result = PurchaseAllocationCalculator.Calculate(
            itemLines: new[]
            {
                Line("A", 1, 100m, manualExpenses: 15m),
                Line("B", 1, 100m, manualExpenses: 5m)
            },
            expenseLines: new[] { new PurchaseExpenseLineInput { ExpenseType = "Other", Amount = 20m } },
            taxableAmountInput: null,
            salesTaxRate: null,
            salesTaxAmountOverride: null,
            salesTaxAllocationMethod: "Proportional",
            expenseAllocationMethod: "Manual",
            manualAdjustment: 0m);

        result.IsReadyToSave.Should().BeTrue();
        result.ValidationErrors.Should().BeEmpty();
    }

    [Fact]
    public void Calculate_manual_allocation_that_does_not_sum_is_flagged_not_ready()
    {
        var result = PurchaseAllocationCalculator.Calculate(
            itemLines: new[]
            {
                Line("A", 1, 100m, manualExpenses: 15m),
                Line("B", 1, 100m, manualExpenses: 1m) // should have been 5 to reconcile with the $20 pool
            },
            expenseLines: new[] { new PurchaseExpenseLineInput { ExpenseType = "Other", Amount = 20m } },
            taxableAmountInput: null,
            salesTaxRate: null,
            salesTaxAmountOverride: null,
            salesTaxAllocationMethod: "Proportional",
            expenseAllocationMethod: "Manual",
            manualAdjustment: 0m);

        result.IsReadyToSave.Should().BeFalse();
        result.ValidationErrors.Should().NotBeEmpty();
        result.Difference.Should().NotBe(0m);
    }

    [Fact]
    public void Calculate_manual_sales_tax_override_replaces_the_calculated_amount()
    {
        var result = PurchaseAllocationCalculator.Calculate(
            itemLines: new[] { Line("Item", 1, 100m) },
            expenseLines: Array.Empty<PurchaseExpenseLineInput>(),
            taxableAmountInput: 100m,
            salesTaxRate: 10m,
            salesTaxAmountOverride: 5m, // calculated would be 10, but manually corrected to 5
            salesTaxAllocationMethod: "Proportional",
            expenseAllocationMethod: "Proportional",
            manualAdjustment: 0m);

        result.SalesTaxAmount.Should().Be(5m);
        result.TotalPurchaseCost.Should().Be(105m);
    }

    [Fact]
    public void Calculate_quantity_5_line_produces_5_unit_cost_bases_summing_to_the_line_total()
    {
        var result = PurchaseAllocationCalculator.Calculate(
            itemLines: new[] { Line("Vintage Book", 5, 2m) }, // $10 line total across 5 units, doesn't divide evenly by cents in odd splits but does here — cover the odd case too
            expenseLines: new[] { new PurchaseExpenseLineInput { ExpenseType = "Other", Amount = 1m } }, // $11 total, 5 units -> not evenly divisible
            taxableAmountInput: null,
            salesTaxRate: null,
            salesTaxAmountOverride: null,
            salesTaxAllocationMethod: "Proportional",
            expenseAllocationMethod: "Proportional",
            manualAdjustment: 0m);

        result.Lines.Should().ContainSingle();
        var line = result.Lines[0];
        line.UnitCostBases.Should().HaveCount(5);
        line.UnitCostBases.Sum().Should().Be(line.FinalLineCostBasis);
        result.PhysicalItemsToCreate.Should().Be(5);
    }

    [Fact]
    public void Calculate_with_no_lines_is_not_ready_to_save()
    {
        var result = PurchaseAllocationCalculator.Calculate(
            itemLines: Array.Empty<PurchaseItemLineInput>(),
            expenseLines: Array.Empty<PurchaseExpenseLineInput>(),
            taxableAmountInput: null,
            salesTaxRate: null,
            salesTaxAmountOverride: null,
            salesTaxAllocationMethod: "Proportional",
            expenseAllocationMethod: "Proportional",
            manualAdjustment: 0m);

        result.IsReadyToSave.Should().BeFalse();
        result.ValidationErrors.Should().Contain(e => e.Contains("хотя бы один"));
    }
}
