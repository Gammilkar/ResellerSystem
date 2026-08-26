using FluentAssertions;
using ResellerSystem.Modules.Inventory.Application;
using Xunit;

namespace ResellerSystem.Modules.Inventory.Tests;

public class MoneyAllocatorTests
{
    [Fact]
    public void AllocateExact_ten_dollars_across_three_equal_weights_never_leaves_a_shortfall()
    {
        var result = MoneyAllocator.AllocateExact(10m, new[] { 1m, 1m, 1m });

        result.Should().BeEquivalentTo(new[] { 3.34m, 3.33m, 3.33m }, o => o.WithStrictOrdering());
        result.Sum().Should().Be(10m);
    }

    [Fact]
    public void AllocateExact_proportional_weights_split_correctly_and_sum_exactly()
    {
        // $50 split between weights 100 and 300 -> 12.50 / 37.50
        var result = MoneyAllocator.AllocateExact(50m, new[] { 100m, 300m });

        result.Should().BeEquivalentTo(new[] { 12.50m, 37.50m }, o => o.WithStrictOrdering());
        result.Sum().Should().Be(50m);
    }

    [Fact]
    public void AllocateExact_single_weight_returns_the_whole_total()
    {
        var result = MoneyAllocator.AllocateExact(123.45m, new[] { 7m });

        result.Should().ContainSingle().Which.Should().Be(123.45m);
    }

    [Fact]
    public void AllocateExact_zero_weights_falls_back_to_equal_split()
    {
        var result = MoneyAllocator.AllocateExact(9m, new[] { 0m, 0m, 0m });

        result.Sum().Should().Be(9m);
        result.Should().OnlyContain(x => x == 3m);
    }

    [Fact]
    public void AllocateExact_empty_weights_returns_empty()
    {
        var result = MoneyAllocator.AllocateExact(10m, Array.Empty<decimal>());

        result.Should().BeEmpty();
    }

    [Fact]
    public void AllocateExact_many_shares_never_drift_from_the_total()
    {
        // A case designed to produce a lot of remainder ties/near-ties.
        var weights = Enumerable.Repeat(1m, 7).ToArray();

        var result = MoneyAllocator.AllocateExact(100m, weights);

        result.Sum().Should().Be(100m);
        result.Should().HaveCount(7);
    }

    [Fact]
    public void AllocateExact_zero_total_returns_all_zeros()
    {
        var result = MoneyAllocator.AllocateExact(0m, new[] { 5m, 10m, 15m });

        result.Should().OnlyContain(x => x == 0m);
    }
}
