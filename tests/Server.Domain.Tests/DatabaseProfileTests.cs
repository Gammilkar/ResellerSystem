using FluentAssertions;
using ResellerSystem.Server.Domain.Entities;
using ResellerSystem.Server.Domain.Enums;
using Xunit;

namespace ResellerSystem.Server.Domain.Tests;

public class DatabaseProfileTests
{
    [Fact]
    public void CreateNew_sets_status_Creating_and_schema_version_zero()
    {
        var profile = DatabaseProfile.CreateNew("Main Business", "reseller_db_000001", "America/Los_Angeles", "USD");

        profile.Status.Should().Be(DatabaseStatus.Creating);
        profile.SchemaVersion.Should().Be(0);
        profile.IsActive.Should().BeTrue();
        profile.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void CreateNew_trims_display_name()
    {
        var profile = DatabaseProfile.CreateNew("  My Business  ", "reseller_db_000002", "UTC", "USD");

        profile.Name.Should().Be("My Business");
    }

    [Fact]
    public void CreateNew_throws_when_physical_name_missing()
    {
        var act = () => DatabaseProfile.CreateNew("Name", "", "UTC", "USD");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Rename_never_changes_physical_database_name()
    {
        var profile = DatabaseProfile.CreateNew("My Business", "reseller_db_000015", "UTC", "USD");
        var originalPhysicalName = profile.PhysicalDatabaseName;

        profile.Rename("SimonSaleStore");

        profile.Name.Should().Be("SimonSaleStore");
        profile.PhysicalDatabaseName.Should().Be(originalPhysicalName);
    }

    [Fact]
    public void MarkReady_sets_status_and_schema_version()
    {
        var profile = DatabaseProfile.CreateNew("Test", "reseller_db_000003", "UTC", "USD");

        profile.MarkReady(1);

        profile.Status.Should().Be(DatabaseStatus.Ready);
        profile.SchemaVersion.Should().Be(1);
    }

    [Fact]
    public void MarkMigrationFailed_never_reports_Ready()
    {
        var profile = DatabaseProfile.CreateNew("Test", "reseller_db_000004", "UTC", "USD");

        profile.MarkMigrationFailed();

        profile.Status.Should().Be(DatabaseStatus.MigrationFailed);
        profile.Status.Should().NotBe(DatabaseStatus.Ready);
    }

    [Fact]
    public void Disable_sets_status_Disabled_and_IsActive_false()
    {
        var profile = DatabaseProfile.CreateNew("Test", "reseller_db_000005", "UTC", "USD");
        profile.MarkReady(1);

        profile.Disable();

        profile.Status.Should().Be(DatabaseStatus.Disabled);
        profile.IsActive.Should().BeFalse();
    }
}
