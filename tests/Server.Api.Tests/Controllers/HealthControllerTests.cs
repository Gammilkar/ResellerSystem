using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using ResellerSystem.Domain.Shared.Dto;
using ResellerSystem.Server.Api.Controllers;
using ResellerSystem.Server.Application.VersionInfo;
using ResellerSystem.Server.Data.Master;
using ResellerSystem.Server.FileStorage;
using Xunit;

namespace ResellerSystem.Server.Api.Tests.Controllers;

public class HealthControllerTests
{
    [Fact]
    public async Task Get_returns_healthy_when_all_checks_pass()
    {
        var masterHealth = Substitute.For<IMasterDatabaseHealthChecker>();
        masterHealth.IsHealthyAsync(Arg.Any<CancellationToken>()).Returns(true);

        var fileStorage = Substitute.For<IFileStorageService>();
        fileStorage.CheckReadWriteAsync(Arg.Any<CancellationToken>()).Returns(true);
        fileStorage.GetAvailableDiskSpaceBytes().Returns(100_000_000_000L);

        var versionProvider = Substitute.For<IVersionProvider>();
        versionProvider.ServerVersion.Returns("0.1.0");

        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns("Development");

        var controller = new HealthController(masterHealth, fileStorage, versionProvider, environment);

        var result = await controller.Get(CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var body = ok.Value.Should().BeOfType<HealthResponse>().Subject;
        body.Status.Should().Be("healthy");
        body.MasterDatabase.Should().Be("healthy");
        body.FileStorage.Should().Be("healthy");
    }

    [Fact]
    public async Task Get_returns_unhealthy_when_master_database_is_down()
    {
        var masterHealth = Substitute.For<IMasterDatabaseHealthChecker>();
        masterHealth.IsHealthyAsync(Arg.Any<CancellationToken>()).Returns(false);

        var fileStorage = Substitute.For<IFileStorageService>();
        fileStorage.CheckReadWriteAsync(Arg.Any<CancellationToken>()).Returns(false);

        var versionProvider = Substitute.For<IVersionProvider>();
        var environment = Substitute.For<IHostEnvironment>();

        var controller = new HealthController(masterHealth, fileStorage, versionProvider, environment);

        var result = await controller.Get(CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var body = ok.Value.Should().BeOfType<HealthResponse>().Subject;
        body.Status.Should().Be("unhealthy");
    }
}
