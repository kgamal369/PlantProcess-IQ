using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PlantProcess.Api.Security;
using Xunit;

namespace PlantProcess.Api.IntegrationTests.Jobs;

public sealed class CanvasJobAccessControlTests
{
    public static IEnumerable<object[]> Cases()
    {
        var routes = new[] {
            ("POST", "job-binding"), ("GET", "job-binding"),
            ("GET", "execution-capability"), ("POST", "runs"),
            ("GET", "runs"), ("GET", "runs/10000000-0000-0000-0000-000000000001") };
        foreach (var (method, suffix) in routes)
        foreach (var state in new[] { "allowed", "denied", "anonymous", "missing-tenant", "cross-tenant" })
            yield return new object[] { method, suffix, state };
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Job_routes_keep_permission_authentication_and_tenant_checks(string method, string suffix, string state)
    {
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Path = "/api/prep/definitions/fixture/" + suffix;
        context.Request.Method = method;
        context.Response.Body = new MemoryStream();
        if (state != "anonymous")
        {
            var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "fixture-user") };
            if (state != "missing-tenant") claims.Add(new Claim("tenant_id", "tenant-a"));
            context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test-principal"));
        }
        if (state == "cross-tenant") context.Request.QueryString = new QueryString("?tenantId=tenant-b");
        var reached = false;
        var resolver = new Resolver(state != "denied");
        var middleware = new AccessControlMiddleware(_ => { reached = true; return Task.CompletedTask; }, NullLogger<AccessControlMiddleware>.Instance);
        await middleware.InvokeAsync(context, resolver);
        Assert.Equal(state == "allowed", reached);
        Assert.Equal(state == "allowed" ? 200 : state == "anonymous" ? 401 : 403, context.Response.StatusCode);
        if (state == "allowed" || state == "denied") Assert.Equal("job.manage", resolver.Requested);
        else Assert.Null(resolver.Requested);
    }

    [Theory]
    [InlineData("/api/prep/definitions/fixture/publish")]
    [InlineData("/api/prep/definitions/fixture/runs/10000000-0000-0000-0000-000000000001/cancel")]
    [InlineData("/api/prep/definitions/fixture/job-binding-extra")]
    public async Task Unrelated_posts_do_not_gain_a_permission_mapping(string path)
    {
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Path = path;
        context.Request.Method = "POST";
        context.Response.Body = new MemoryStream();
        var reached = false;
        var resolver = new Resolver(true);
        var middleware = new AccessControlMiddleware(_ => { reached = true; return Task.CompletedTask; }, NullLogger<AccessControlMiddleware>.Instance);
        await middleware.InvokeAsync(context, resolver);
        Assert.False(reached);
        Assert.Equal(403, context.Response.StatusCode);
        Assert.Null(resolver.Requested);
    }

    // Only the permission decision is controlled; the production middleware is executed unchanged.
    // The separate real-host HTTP suite uses the real resolver and login.
    private sealed class Resolver(bool allow) : IPlantEntitlementResolver
    {
        public string? Requested { get; private set; }
        public bool HasPermission(ClaimsPrincipal user, string permissionCode)
        { Requested = permissionCode; return allow; }
        public EffectiveEntitlementDto Resolve(ClaimsPrincipal user) => new(
            "tenant-a", "fixture", "Viewer", "Viewer", "Light",
            Array.Empty<string>(), Array.Empty<string>(), new Dictionary<string, string>());
    }
}
