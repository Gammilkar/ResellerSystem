using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using ResellerSystem.Domain.Shared.Dto;
using ResellerSystem.Domain.Shared.Enums;
using ResellerSystem.Server.Api.Controllers;
using ResellerSystem.Server.Application.Databases;
using ResellerSystem.Server.Application.Exceptions;
using Xunit;

namespace ResellerSystem.Server.Api.Tests.Controllers;

public class DatabasesControllerTests
{
    private readonly IDatabaseProvisioningService _service = Substitute.For<IDatabaseProvisioningService>();
    private readonly DatabasesController _controller;

    public DatabasesControllerTests()
    {
        _controller = new DatabasesController(_service);
    }

    [Fact]
    public async Task List_returns_all_databases()
    {
        var databases = new List<DatabaseProfileDto>
        {
            Make("Main Business"),
            Make("Daria")
        };
        _service.ListAsync(Arg.Any<CancellationToken>()).Returns(databases);

        var result = await _controller.List(CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeEquivalentTo(databases);
    }

    [Fact]
    public async Task GetById_returns_database_when_found()
    {
        var dto = Make("Test");
        _service.GetAsync(dto.Id, Arg.Any<CancellationToken>()).Returns(dto);

        var result = await _controller.GetById(dto.Id, CancellationToken.None);

        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetById_propagates_NotFoundException_for_invalid_id()
    {
        var missingId = Guid.NewGuid();
        _service.GetAsync(missingId, Arg.Any<CancellationToken>())
            .Returns<DatabaseProfileDto>(_ => throw new NotFoundException("DATABASE_NOT_FOUND", "Database was not found."));

        var act = async () => await _controller.GetById(missingId, CancellationToken.None);

        // The controller itself doesn't catch this — ExceptionHandlingMiddleware
        // does, at the pipeline level (see Middleware/ExceptionHandlingMiddlewareTests).
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Create_returns_201_with_created_database()
    {
        var request = new CreateDatabaseRequest { Name = "New Business", TimeZone = "UTC", Currency = "USD" };
        var created = Make("New Business");
        _service.CreateAsync(request, Arg.Any<CancellationToken>()).Returns(created);

        var result = await _controller.Create(request, CancellationToken.None);

        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.Value.Should().Be(created);
    }

    [Fact]
    public async Task Update_renames_and_returns_updated_dto()
    {
        var dto = Make("SimonSaleStore");
        var request = new UpdateDatabaseRequest { Name = "SimonSaleStore" };
        _service.UpdateAsync(dto.Id, request, Arg.Any<CancellationToken>()).Returns(dto);

        var result = await _controller.Update(dto.Id, request, CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ((DatabaseProfileDto)ok.Value!).Name.Should().Be("SimonSaleStore");
    }

    private static DatabaseProfileDto Make(string name) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        TimeZone = "UTC",
        Currency = "USD",
        Status = DatabaseStatusDto.Ready,
        IsActive = true,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };
}
