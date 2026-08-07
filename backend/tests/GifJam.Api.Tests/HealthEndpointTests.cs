using System.Net;
using System.Net.Http.Json;
using GifJam.Api.Tests.Infrastructure;

namespace GifJam.Api.Tests;

public sealed class HealthEndpointTests(GifJamApiFactory factory) : IClassFixture<GifJamApiFactory>
{
    [Fact]
    public async Task LiveReturnsHealthyStatus()
    {
        using var client = factory.CreateClient(new() { BaseAddress = new("https://localhost") });

        using var response = await client.GetAsync("/health/live");
        var payload = await response.Content.ReadFromJsonAsync<HealthResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal("Healthy", payload.Status);
    }

    [Fact]
    public async Task OpenApiIsAvailableInDevelopment()
    {
        using var client = factory.CreateClient(new() { BaseAddress = new("https://localhost") });

        using var response = await client.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private sealed record HealthResponse(string Status);
}
