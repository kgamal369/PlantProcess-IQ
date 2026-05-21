using PlantProcess.Api.Extensions;
using PlantProcess.Application.Demo.Interfaces;

namespace PlantProcess.Api.Endpoints.Demo;

public static class DemoLifecycleEndpoints
{
    public static IEndpointRouteBuilder MapDemoLifecycleEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/demo")
            .WithTags("Demo Lifecycle")
            .RequireAuthorization("PlantProcessViewer");

        group.MapGet("/lifecycle", GetLifecycleAsync)
            .WithSummary("Get the complete PlantProcess IQ demo lifecycle")
            .WithDescription("Returns the real controlled lifecycle: license -> connector -> staging -> schema mapping -> jobs -> dashboard -> risk/correlation -> ML readiness -> final report.");

        return app;
    }

    private static async Task<IResult> GetLifecycleAsync(
        IDemoLifecycleService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetDemoLifecycleAsync(cancellationToken);
        return result.ToHttpResult(Results.Ok);
    }
}