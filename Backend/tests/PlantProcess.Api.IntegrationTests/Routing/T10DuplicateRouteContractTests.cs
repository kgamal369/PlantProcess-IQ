// PPIQ-T10 - no two endpoints may register the same route pattern + HTTP method.
// ASP.NET resolves such collisions by registration order, which silently shadows one
// implementation behind the other - the worst class of "works on my branch" bug for a
// 2,000-file endpoint surface. Enumerated straight from EndpointDataSource, so every
// MapGroup/Map* in the app is covered automatically, including future ones.
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace PlantProcess.Api.IntegrationTests.Routing;

[Trait("Task", "T10")]
[Trait("Category", "Integration")]
public sealed class T10DuplicateRouteContractTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public T10DuplicateRouteContractTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public void No_route_pattern_and_method_is_registered_twice()
    {
        using var scope = _factory.Services.CreateScope();
        var sources = scope.ServiceProvider.GetServices<EndpointDataSource>();

        var keys = sources
            .SelectMany(s => s.Endpoints)
            .OfType<RouteEndpoint>()
            .SelectMany(e =>
            {
                var methods = e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods
                              ?? new[] { "(any)" };
                return methods.Select(m => $"{m} {e.RoutePattern.RawText}");
            })
            .GroupBy(k => k, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key} (x{g.Count()})")
            .ToList();

        Assert.True(keys.Count == 0,
            "PPIQ-T10: duplicate route registrations detected - one implementation is " +
            "silently shadowing another:\n" + string.Join("\n", keys));
    }
}