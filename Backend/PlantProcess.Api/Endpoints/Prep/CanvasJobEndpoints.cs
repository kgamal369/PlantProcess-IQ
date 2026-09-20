using Microsoft.Extensions.Hosting;
using PlantProcess.Api.ErrorHandling;
using PlantProcess.Application.Definitions;
using PlantProcess.Application.Integration.Interfaces.Jobs;
using PlantProcess.Application.Jobs.Canvas;

namespace PlantProcess.Api.Endpoints.Prep;

/// <summary>
/// THE CANVAS CONSUMER SURFACE OVER THE GOVERNED JOB MODEL (T-245).
///
/// Everything here addresses a definition by the code its tenant owns. No handler takes
/// a job id or a definition id from the caller, because a client-supplied identifier is
/// a request, not an entitlement: the server resolves identity from the tenant claim and
/// refuses anything that does not belong to it.
///
/// There is no scheduler, no run store and no executor here. Capability comes from the
/// capability authority and the executor's own admission, launching goes through the
/// existing orchestrator, and evidence is read back from the run authority.
/// </summary>
public static class CanvasJobEndpoints
{
    public static IEndpointRouteBuilder MapCanvasJobEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/prep/definitions")
            .WithTags("Prep - Canvas execution")
            .RequireAuthorization();

        group.MapGet("/{code}/execution-capability", DescribeCapabilityAsync)
            .WithSummary("Static execution eligibility for a Canvas definition")
            .WithDescription("Answers whether an executor exists and whether this exact version is statically eligible. It reserves nothing and does not guarantee that a later launch is admitted.");

        group.MapPost("/{code}/job-binding", BindAsync)
            .WithSummary("Bind a published Canvas version to its governed job")
            .WithDescription("Creates or updates the one job that targets this definition. Repeating the request addresses the same job and preserves its schedule and pool settings.");

        group.MapGet("/{code}/job-binding", GetBindingAsync)
            .WithSummary("The governed job bound to this definition");

        group.MapPost("/{code}/runs", LaunchAsync)
            .WithSummary("Launch the bound job for this definition")
            .WithDescription("Runs through the normal job orchestrator. Execution is owned by the server and does not inherit the browser's lifetime.");

        group.MapGet("/{code}/runs", FindRunAsync)
            .WithSummary("The run a launch created, by its correlation")
            .WithDescription("Attaches the monitor to exactly the launch that started it, never to the newest run of the job.");

        group.MapGet("/{code}/runs/{runId:guid}", ReadRunAsync)
            .WithSummary("Persisted run metadata and per-block evidence");

        return app;
    }

    public sealed record BindCanvasJobRequest(int? PinnedVersion, string? JobName);

    public sealed record LaunchCanvasRunRequest(string CorrelationId, string? RequestedBy);

    private static async Task<IResult> DescribeCapabilityAsync(
        string code,
        int? version,
        HttpContext context,
        ICanvasJobBindingService canvasJobs,
        ICanonicalIdentityResolver identity,
        CancellationToken cancellationToken)
    {
        Guid tenantId = await ResolveTenantAsync(context, identity, cancellationToken);

        var result = await canvasJobs.DescribeExecutionAsync(tenantId, code, version, cancellationToken);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Problem(result.Error!);
    }

    private static async Task<IResult> BindAsync(
        string code,
        BindCanvasJobRequest request,
        HttpContext context,
        ICanvasJobBindingService canvasJobs,
        ICanonicalIdentityResolver identity,
        CancellationToken cancellationToken)
    {
        Guid tenantId = await ResolveTenantAsync(context, identity, cancellationToken);

        var result = await canvasJobs.BindAsync(
            tenantId,
            new CanvasJobBindingRequest(code, request?.PinnedVersion, request?.JobName),
            cancellationToken);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Problem(result.Error!);
    }

    private static async Task<IResult> GetBindingAsync(
        string code,
        HttpContext context,
        ICanvasJobBindingService canvasJobs,
        ICanonicalIdentityResolver identity,
        CancellationToken cancellationToken)
    {
        Guid tenantId = await ResolveTenantAsync(context, identity, cancellationToken);

        var result = await canvasJobs.FindBindingAsync(tenantId, code, cancellationToken);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Problem(result.Error!);
    }

    /// <summary>
    /// A RUN IS NOT THE BROWSER'S. This handler takes NO CancellationToken parameter on
    /// purpose: the injected one is HttpContext.RequestAborted, so closing a tab or
    /// losing a connection would cancel a governed execution that the server owns. The
    /// run is bounded by the host's own stopping token instead, and cancellation stays
    /// an explicit, authorized job operation.
    /// </summary>
    private static async Task<IResult> LaunchAsync(
        string code,
        LaunchCanvasRunRequest request,
        HttpContext context,
        ICanvasJobBindingService canvasJobs,
        ICanonicalIdentityResolver identity,
        IJobRunOrchestratorService orchestrator,
        IHostApplicationLifetime lifetime)
    {
        CancellationToken serverOwned = lifetime.ApplicationStopping;

        if (request is null || string.IsNullOrWhiteSpace(request.CorrelationId))
        {
            return ApplicationProblems.Validation("A launch must carry the correlation the monitor will attach to.");
        }

        Guid tenantId = await ResolveTenantAsync(context, identity, serverOwned);

        var binding = await canvasJobs.FindBindingAsync(tenantId, code, serverOwned);
        if (binding.IsFailure || binding.Value is null)
        {
            return Problem(binding.Error!);
        }

        var launched = await orchestrator.RunNowAsync(
            binding.Value.JobDefinitionId,
            request.RequestedBy,
            request.CorrelationId,
            serverOwned);

        var evidence = await canvasJobs.FindRunByCorrelationAsync(tenantId, code, request.CorrelationId, serverOwned);

        if (evidence.IsSuccess && evidence.Value is not null)
        {
            return Results.Ok(evidence.Value);
        }

        // The orchestrator refused before a run existed. The refusal is the answer, and
        // no run is invented to carry it.
        return launched.IsFailure
            ? Problem(launched.Error!)
            : Problem(evidence.Error
                ?? PlantProcess.Application.Common.Results.ApplicationError.Unexpected(
                    "The launch reported success and no run was recorded."));
    }

    private static async Task<IResult> FindRunAsync(
        string code,
        string correlationId,
        HttpContext context,
        ICanvasJobBindingService canvasJobs,
        ICanonicalIdentityResolver identity,
        CancellationToken cancellationToken)
    {
        Guid tenantId = await ResolveTenantAsync(context, identity, cancellationToken);

        var result = await canvasJobs.FindRunByCorrelationAsync(tenantId, code, correlationId, cancellationToken);

        if (result.IsFailure)
        {
            return Problem(result.Error!);
        }

        // No run yet is a real answer while a launch is still being admitted. It is not
        // an error and it is certainly not someone else's run.
        return result.Value is null ? Results.NoContent() : Results.Ok(result.Value);
    }

    private static async Task<IResult> ReadRunAsync(
        string code,
        Guid runId,
        HttpContext context,
        ICanvasJobBindingService canvasJobs,
        ICanonicalIdentityResolver identity,
        CancellationToken cancellationToken)
    {
        Guid tenantId = await ResolveTenantAsync(context, identity, cancellationToken);

        var result = await canvasJobs.ReadRunAsync(tenantId, code, runId, cancellationToken);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Problem(result.Error!);
    }

    /// <summary>
    /// One typed refusal becomes one HTTP status. The code the authority produced is
    /// carried through, because a caller that cannot see JOB_EXEC_EXECUTOR_MISSING
    /// cannot explain to a person why a launch was refused.
    /// </summary>
    private static IResult Problem(PlantProcess.Application.Common.Results.ApplicationError error)
    {
        string detail = error.Message;
        string code = string.IsNullOrWhiteSpace(error.Code) ? "error" : error.Code;

        return error.Type switch
        {
            PlantProcess.Application.Common.Results.ApplicationErrorType.Validation => ApplicationProblems.Validation(detail, code),
            PlantProcess.Application.Common.Results.ApplicationErrorType.NotFound => ApplicationProblems.NotFound(detail, code),
            PlantProcess.Application.Common.Results.ApplicationErrorType.Conflict => ApplicationProblems.Conflict(detail, code),
            PlantProcess.Application.Common.Results.ApplicationErrorType.Unauthorized => ApplicationProblems.Unauthorized(detail, code),
            PlantProcess.Application.Common.Results.ApplicationErrorType.Forbidden => ApplicationProblems.Forbidden(detail, code),
            PlantProcess.Application.Common.Results.ApplicationErrorType.BusinessRule => ApplicationProblems.Conflict(detail, code),
            _ => ApplicationProblems.Internal(detail, code),
        };
    }

    /// <summary>
    /// The tenant comes from the caller's claim, exactly as the rest of the prep surface
    /// reads it. When a deployment carries no claim, the identity authority answers with
    /// the single tenant, and refuses when more than one exists rather than choosing.
    /// </summary>
    private static async Task<Guid> ResolveTenantAsync(
        HttpContext context,
        ICanonicalIdentityResolver identity,
        CancellationToken cancellationToken)
    {
        if (Guid.TryParse(context.User?.FindFirst("tenant_id")?.Value, out Guid claimed) && claimed != Guid.Empty)
        {
            return claimed;
        }

        Guid? resolved = await identity.ResolveTenantAsync(null, cancellationToken);
        return resolved ?? Guid.Empty;
    }
}
