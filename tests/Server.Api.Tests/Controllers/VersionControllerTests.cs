using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using ResellerSystem.Domain.Shared.Dto;
using ResellerSystem.Server.Api.Controllers;
using ResellerSystem.Server.Application.VersionInfo;
using Xunit;

namespace ResellerSystem.Server.Api.Tests.Controllers;

public class VersionControllerTests
{
    [Fact]
    public void Get_returns_version_from_provider()
    {
        var expected = new VersionResponse
        {
            ServerVersion = "0.1.0",
            ApiVersion = "1",
            TenantSchemaVersion = 1,
            MasterSchemaVersion = 1,
            MinimumDesktopClientVersion = "0.1.0",
            MinimumAndroidClientVersion = "0.1.0"
        };

        var versionProvider = Substitute.For<IVersionProvider>();
        versionProvider.GetVersion().Returns(expected);

        var controller = new VersionController(versionProvider);

        var result = controller.Get();

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeSameAs(expected);
    }
}
