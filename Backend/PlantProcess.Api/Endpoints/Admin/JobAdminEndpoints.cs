using Microsoft.EntityFrameworkCore;
using PlantProcess.Application.Contracts.Integration;
using PlantProcess.Application.Services.Integration;
using PlantProcess.Domain.Enums.Integration;
using PlantProcess.Infrastructure.Persistence;

namespace PlantProcess.Api.Endpoints.Admin;

public static class JobAdminEndpoints
{
    public static IEndpointRouteBuilder MapJobAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin/jobs")
            .WithTags("Admin - Jobs");

        group.MapPost("/{jobId:guid}/run-now", RunNowAsync)
            .WithSummary("Run a job immediately")
            .WithDescription("Triggers immediate execution for supported jobs and records JobRunHistory.");

        group.MapPost("/{jobId:guid}/pause", PauseAsync)
            .WithSummary("Pause a job")
            .WithDescription("Disables a job so scheduled runs will not execute.");

        group.MapPost("/{jobId:guid}/resume", ResumeAsync)
            .WithSummary("Resume a job")
            .WithDescription("Enables a paused job.");

        group.MapGet("/{jobId:guid}/history", GetHistoryAsync)
            .WithSummary("Get job run history")
            .WithDescription("Returns the latest job run history records.");

        group.MapPatch("/connection-profiles/{connectionProfileId:guid}/schedule", UpdateConnectionScheduleAsync)
            .WithSummary("Update DB Link import schedule")
            .WithDescription("Stores import schedule on ConnectionProfile and upserts a DbLinkImport JobDefinition.");

        group.MapPatch("/mappings/{mappingDefinitionId:guid}/schedule", UpdateMappingScheduleAsync)
            .WithSummary("Update canonical refresh schedule")
            .WithDescription("Upserts a CanonicalRefresh JobDefinition for a mapping definition.");

        return app;
    }

    private static async Task<IResult> RunNowAsync(
        Guid jobId,
        RunJobNowRequest request,
        IJobRunOrchestratorService orchestrator,
        CancellationToken cancellationToken)
    {
        var result = await orchestrator.RunNowAsync(
            jobId,
            request.RequestedBy,
            request.CorrelationId,
            cancellationToken);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : ToProblem(result.Error!.Message, result.Error.Type.ToString());
    }

    private static async Task<IResult> PauseAsync(
        Guid jobId,
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var job = await dbContext.JobDefinitions
            .FirstOrDefaultAsync(x => !x.IsDeleted && x.Id == jobId, cancellationToken);

        if (job is null)
            return Results.NotFound(new { message = "Job definition was not found." });

        job.Disable();

        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Ok(new JobActionResponseDto(
            job.Id,
            job.JobCode,
            job.JobName,
            job.JobType,
            job.LastRunStatus,
            "Job paused. Scheduled runs are disabled.",
            null,
            DateTime.UtcNow));
    }

    private static async Task<IResult> ResumeAsync(
        Guid jobId,
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var job = await dbContext.JobDefinitions
            .FirstOrDefaultAsync(x => !x.IsDeleted && x.Id == jobId, cancellationToken);

        if (job is null)
            return Results.NotFound(new { message = "Job definition was not found." });

        job.Enable();

        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Ok(new JobActionResponseDto(
            job.Id,
            job.JobCode,
            job.JobName,
            job.JobType,
            job.LastRunStatus,
            "Job resumed. Scheduled runs are enabled.",
            null,
            DateTime.UtcNow));
    }

    private static async Task<IResult> GetHistoryAsync(
        Guid jobId,
        int? take,
        IJobRuntimeService jobRuntimeService,
        CancellationToken cancellationToken)
    {
        var result = await jobRuntimeService.GetHistoryAsync(
            jobId,
            take ?? 20,
            cancellationToken);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : ToProblem(result.Error!.Message, result.Error.Type.ToString());
    }

    private static async Task<IResult> UpdateConnectionScheduleAsync(
        Guid connectionProfileId,
        UpdateConnectionImportScheduleRequest request,
        PlantProcessDbContext dbContext,
        IJobRegistrationService jobRegistrationService,
        CancellationToken cancellationToken)
    {
        var connection = await dbContext.ConnectionProfiles
            .FirstOrDefaultAsync(x => !x.IsDeleted && x.Id == connectionProfileId, cancellationToken);

        if (connection is null)
            return Results.NotFound(new { message = "Connection profile was not found." });

        connection.UpdateImportSchedule(
            request.ScheduleExpression,
            request.ImportIntervalMinutes);

        await dbContext.SaveChangesAsync(cancellationToken);

        var jobCode = $"CONNECTION_IMPORT_{connection.ConnectionProfileCode}";

        var upsertResult = await jobRegistrationService.UpsertJobAsync(
            new UpsertJobDefinitionRequest(
                JobCode: jobCode,
                JobName: $"DB Link Import - {connection.ConnectionProfileName}",
                JobType: JobDefinitionType.DbLinkImport,
                TargetId: connection.Id,
                TargetType: "ConnectionProfile",
                ScheduleExpression: request.ScheduleExpression,
                IsEnabled: connection.IsActive,
                Description: $"Scheduled import job for connection profile {connection.ConnectionProfileCode}.",
                IsSynthetic: connection.IsSynthetic,
                SourceSystem: "PlantProcessIQ.Admin",
                SourceRecordId: jobCode),
            cancellationToken);

        return upsertResult.IsSuccess
            ? Results.Ok(upsertResult.Value)
            : ToProblem(upsertResult.Error!.Message, upsertResult.Error.Type.ToString());
    }

    private static async Task<IResult> UpdateMappingScheduleAsync(
        Guid mappingDefinitionId,
        UpdateMappingRefreshScheduleRequest request,
        PlantProcessDbContext dbContext,
        IJobRegistrationService jobRegistrationService,
        CancellationToken cancellationToken)
    {
        var mapping = await dbContext.MappingDefinitions
            .FirstOrDefaultAsync(x => !x.IsDeleted && x.Id == mappingDefinitionId, cancellationToken);

        if (mapping is null)
            return Results.NotFound(new { message = "Mapping definition was not found." });

        var jobCode = $"CANONICAL_REFRESH_{mapping.MappingCode}";

        var upsertResult = await jobRegistrationService.UpsertJobAsync(
            new UpsertJobDefinitionRequest(
                JobCode: jobCode,
                JobName: $"Canonical Refresh - {mapping.MappingName}",
                JobType: JobDefinitionType.CanonicalRefresh,
                TargetId: mapping.Id,
                TargetType: "MappingDefinition",
                ScheduleExpression: request.ScheduleExpression,
                IsEnabled: mapping.IsActive,
                Description: $"Scheduled canonical refresh job for mapping {mapping.MappingCode}.",
                IsSynthetic: mapping.IsSynthetic,
                SourceSystem: "PlantProcessIQ.Admin",
                SourceRecordId: jobCode),
            cancellationToken);

        return upsertResult.IsSuccess
            ? Results.Ok(upsertResult.Value)
            : ToProblem(upsertResult.Error!.Message, upsertResult.Error.Type.ToString());
    }

    private static IResult ToProblem(string message, string code)
    {
        return Results.Problem(
            detail: message,
            title: code,
            statusCode: code.Contains("NotFound", StringComparison.OrdinalIgnoreCase) ? 404 : 400);
    }
}