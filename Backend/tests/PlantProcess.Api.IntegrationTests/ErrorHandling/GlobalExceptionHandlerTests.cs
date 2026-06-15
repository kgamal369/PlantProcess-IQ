using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using PlantProcess.Api.ErrorHandling;
using Xunit;

namespace PlantProcess.Api.IntegrationTests.ErrorHandling;

public sealed class GlobalExceptionHandlerTests
{
    private sealed class FakeEnv : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "PlantProcess.Api";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; }
            = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }

    private static async Task<(int status, string contentType, JsonElement root)> Run(Exception ex, string env)
    {
        var handler = new GlobalExceptionHandler(
            new FakeEnv { EnvironmentName = env },
            NullLogger<GlobalExceptionHandler>.Instance);

        var ctx = new DefaultHttpContext();
        ctx.Request.Path = "/test/throw";
        ctx.Items["CorrelationId"] = "test-correlation-id";
        ctx.Response.Body = new MemoryStream();

        var handled = await handler.TryHandleAsync(ctx, ex, CancellationToken.None);
        Assert.True(handled);

        ctx.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(ctx.Response.Body);
        var json = await reader.ReadToEndAsync();
        using var doc = JsonDocument.Parse(json);
        return (ctx.Response.StatusCode, ctx.Response.ContentType ?? "", doc.RootElement.Clone());
    }

    [SkippableFact]
    public async Task Unhandled_exception_maps_to_500_problem_json_with_traceId_and_no_stack_in_production()
    {
        var (status, contentType, root) = await Run(new InvalidOperationException("boom"), "Production");

        Assert.Equal(500, status);
        Assert.StartsWith("application/problem+json", contentType);
        Assert.True(root.TryGetProperty("type", out _));
        Assert.True(root.TryGetProperty("title", out _));
        Assert.Equal(500, root.GetProperty("status").GetInt32());
        Assert.Equal("test-correlation-id", root.GetProperty("traceId").GetString());
        Assert.Equal("internal", root.GetProperty("errorCode").GetString());
        Assert.False(root.TryGetProperty("exception", out _)); // no stack trace in Production
        Assert.False(root.TryGetProperty("detail", out var d) && d.ValueKind == JsonValueKind.String);
    }

    [SkippableFact]
    public async Task BadHttpRequestException_maps_to_400_problem_json()
    {
        var (status, contentType, root) = await Run(new BadHttpRequestException("bad body", 400), "Production");

        Assert.Equal(400, status);
        Assert.StartsWith("application/problem+json", contentType);
        Assert.Equal(400, root.GetProperty("status").GetInt32());
        Assert.Equal("bad_request", root.GetProperty("errorCode").GetString());
    }

    [SkippableFact]
    public async Task Development_includes_detail_and_exception()
    {
        var (_, _, root) = await Run(new InvalidOperationException("boom-dev"), "Development");
        Assert.Equal("boom-dev", root.GetProperty("detail").GetString());
        Assert.True(root.TryGetProperty("exception", out _));
    }
}
