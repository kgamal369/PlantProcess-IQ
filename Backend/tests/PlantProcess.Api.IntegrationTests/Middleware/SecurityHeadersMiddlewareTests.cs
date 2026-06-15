using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using PlantProcess.Api.Middleware;
using Xunit;

namespace PlantProcess.Api.IntegrationTests.Middleware;

public sealed class SecurityHeadersMiddlewareTests
{
    [SkippableFact]
    public async Task Sets_baseline_security_headers()
    {
        var config = new ConfigurationBuilder().Build();
        var mw = new SecurityHeadersMiddleware(_ => Task.CompletedTask, config);
        var ctx = new DefaultHttpContext();

        await mw.InvokeAsync(ctx);

        var h = ctx.Response.Headers;
        Assert.Equal("nosniff", h["X-Content-Type-Options"]);
        Assert.Equal("DENY", h["X-Frame-Options"]);
        Assert.Equal("no-referrer", h["Referrer-Policy"]);
        Assert.True(h.ContainsKey("Content-Security-Policy"));
        // NOTE: Strict-Transport-Security is added by app.UseHsts() over HTTPS in
        // non-Development environments and is verified by the e2e https smoke run.
    }
}
