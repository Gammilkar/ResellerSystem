using FluentAssertions;
using ResellerSystem.Server.Modules.Abstractions;
using Xunit;

namespace ResellerSystem.Server.Data.Tests;

// Lives here (not a dedicated Server.Modules.Abstractions.Tests project)
// because Server.Data.Tests already references Server.Modules.Abstractions
// and this is a pure, fast unit test with no Postgres dependency — adding
// a whole new test project for one small value type would be unnecessary
// ceremony at this stage.
public class SemanticVersionTests
{
    [Theory]
    [InlineData("1.2.3", 1, 2, 3)]
    [InlineData("0.1.0", 0, 1, 0)]
    public void TryParse_parses_valid_versions(string input, int major, int minor, int patch)
    {
        var ok = SemanticVersion.TryParse(input, out var result);

        ok.Should().BeTrue();
        result.Should().Be(new SemanticVersion(major, minor, patch));
    }

    [Theory]
    [InlineData("1.2")]
    [InlineData("1.2.3.4")]
    [InlineData("a.b.c")]
    [InlineData("")]
    [InlineData(null)]
    public void TryParse_rejects_invalid_versions(string? input)
    {
        var ok = SemanticVersion.TryParse(input, out _);

        ok.Should().BeFalse();
    }

    [Fact]
    public void Comparison_operators_order_by_major_then_minor_then_patch()
    {
        var v1 = SemanticVersion.Parse("1.0.0");
        var v2 = SemanticVersion.Parse("1.1.0");
        var v3 = SemanticVersion.Parse("1.1.1");
        var v4 = SemanticVersion.Parse("2.0.0");

        (v1 < v2).Should().BeTrue();
        (v2 < v3).Should().BeTrue();
        (v3 < v4).Should().BeTrue();
        (v4 > v1).Should().BeTrue();
        (v1 <= SemanticVersion.Parse("1.0.0")).Should().BeTrue();
    }

    [Fact]
    public void Parse_throws_FormatException_for_invalid_input()
    {
        var act = () => SemanticVersion.Parse("not-a-version");

        act.Should().Throw<FormatException>();
    }
}
