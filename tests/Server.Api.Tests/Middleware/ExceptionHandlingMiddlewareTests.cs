using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using FluentAssertions;
using ResellerSystem.Domain.Shared.Dto;
using ResellerSystem.Server.Api.Middleware;
using ResellerSystem.Server.Application.Exceptions;
using Xunit;

namespace ResellerSystem.Server.Api.Tests.Middleware;

public class ExceptionHandlingMiddlewareTests
{
    private static async Task<(HttpStatusCode StatusCode, ApiErrorResponse Body)> RunAsync(Exception thrown, string environment = "Production")
    {
        using var host = await new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost.UseTestServer();
                webHost.UseEnvironment(environment);
                webHost.Configure(app =>
                {
                    app.UseMiddleware<ExceptionHandlingMiddleware>();
                    app.Run(_ => throw thrown);
                });
            })
            .StartAsync();

        var client = host.GetTestClient();
        var response = await client.GetAsync("/");
        var body = await response.Content.ReadFromJsonAsync<ApiErrorResponse>(new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        return (response.StatusCode, body!);
    }

    [Fact]
    public async Task NotFoundException_maps_to_404_with_code()
    {
        var (statusCode, body) = await RunAsync(new NotFoundException("DATABASE_NOT_FOUND", "Database was not found."));

        statusCode.Should().Be(HttpStatusCode.NotFound);
        body.Error.Code.Should().Be("DATABASE_NOT_FOUND");
    }

    [Fact]
    public async Task ValidationFailedException_maps_to_400_with_details()
    {
        var (statusCode, body) = await RunAsync(new ValidationFailedException(new[] { "Name is required." }));

        statusCode.Should().Be(HttpStatusCode.BadRequest);
        body.Error.Details.Should().Contain("Name is required.");
    }

    [Fact]
    public async Task Unhandled_exception_maps_to_500_and_hides_message_in_production()
    {
        var (statusCode, body) = await RunAsync(new InvalidOperationException("connection string leaked here"), environment: "Production");

        statusCode.Should().Be(HttpStatusCode.InternalServerError);
        body.Error.Message.Should().NotContain("connection string");
        body.Error.Code.Should().Be("INTERNAL_ERROR");
    }
}
