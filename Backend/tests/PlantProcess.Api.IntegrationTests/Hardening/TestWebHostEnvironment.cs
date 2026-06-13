using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;

namespace PlantProcess.Api.IntegrationTests.Hardening;

// Minimal IWebHostEnvironment double so the validator can be unit tested for a
// chosen environment without booting a host.
internal sealed class TestWebHostEnvironment : IWebHostEnvironment
{
    public string EnvironmentName { get; set; } = "Production";
    public string ApplicationName { get; set; } = "PlantProcess.Api.Tests";
    public string ContentRootPath { get; set; } = System.AppContext.BaseDirectory;
    public string WebRootPath { get; set; } = System.AppContext.BaseDirectory;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
}