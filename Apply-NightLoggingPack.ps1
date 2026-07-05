& {
# ================================================================================================
# PPIQ NIGHT PACK: V1-44 (hourly system log) + V1-45 (customer job-event log, complete)
#                  + V1-42 HMI unblock (results endpoint engine filter) + V1-47 (fresh-start tool)
# ================================================================================================
# WHAT LANDS:
#  V1-44  Serilog: systemlog_yyyyMMddHH.log hourly (was plantprocess-api- daily), retention 336,
#         file level Information+ outside Development (Verbose in dev) - config by environment.
#  V1-45  job_log table (script 252, applied live + committed for fresh installs);
#         JobLogService (DB row + mirrored Serilog event); JobLogEndpointFilter wraps
#         Stage-1 / Stage-2 / full-cycle / connector-test endpoints with Started/Completed/
#         Failed events (behavior untouched, errors rethrown); GET /admin/job-logs with
#         jobType/jobName/severity/day filters, paged 500; joblog_yyyyMMddHH.log hourly via a
#         filtered sub-logger. => the two-files-per-hour requirement, live.
#  V1-42  /analysis results run-resolution now includes ppiql-deterministic-core-v1, so the
#         CorrelationPage renders the 13 findings (planted superheat driver on top).
#  V1-47  tools\reset-app-database.ps1: guarded day-one reset (drop -> EF -> numbered SQL),
#         Admin Golden Rule preserved (sysadmin only, via FirstRunProvisioning at API start).
# Gates: stop API -> psql script -> dotnet build -> dotnet test. Commit gated on PPIQ_COMMIT=1.
# ================================================================================================
$ErrorActionPreference = 'Stop'
$RepoRoot = 'C:\Workspace\PlantProcess-IQ'
$enc = New-Object System.Text.UTF8Encoding($false)
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$backupDir = Join-Path $RepoRoot ('deploy\.ppiq-backups\night-logging-' + $stamp)
New-Item -ItemType Directory -Path $backupDir -Force | Out-Null

# ---------------------------------------------------------------- [1/5] backups
Write-Host '[1/5] Backups'
$touched = @(
 'Backend\PlantProcess.Api\Program.cs',
 'Backend\PlantProcess.Api\Endpoints\Admin\TwoStageImportEndpoints.cs',
 'Backend\PlantProcess.Api\Endpoints\Admin\ConnectorAdminEndpoints.cs',
 'Backend\PlantProcess.Api\Endpoints\Admin\AdminEndpoints.cs',
 'Backend\PlantProcess.Api\Endpoints\Analytics\AdvancedResultsEndpoints.cs')
foreach ($rel in $touched) {
    $dest = Join-Path $backupDir $rel
    New-Item -ItemType Directory -Path (Split-Path $dest) -Force | Out-Null
    Copy-Item (Join-Path $RepoRoot $rel) $dest -Force
}

# ---------------------------------------------------------------- [2/5] anchored edits
Write-Host '[2/5] Anchored edits (refuse-if-diverged, idempotent)'
$editsJson = @'
[
 {
  "file": "Backend\\PlantProcess.Api\\Program.cs",
  "anchor": "var logFilePath = Path.Combine(logDirectory, \"plantprocess-api-.log\");",
  "new": "var logFilePath = Path.Combine(logDirectory, \"systemlog_.log\");\nvar jobLogFilePath = Path.Combine(logDirectory, \"joblog_.log\");\nvar fileMinLevel = string.Equals(Environment.GetEnvironmentVariable(\"ASPNETCORE_ENVIRONMENT\"), \"Development\", StringComparison.OrdinalIgnoreCase)\n    ? LogEventLevel.Verbose\n    : LogEventLevel.Information;",
  "marker": "systemlog_.log"
 },
 {
  "file": "Backend\\PlantProcess.Api\\Program.cs",
  "anchor": "        rollingInterval: RollingInterval.Day,",
  "new": "        rollingInterval: RollingInterval.Hour,",
  "marker": "rollingInterval: RollingInterval.Hour,"
 },
 {
  "file": "Backend\\PlantProcess.Api\\Program.cs",
  "anchor": "        retainedFileCountLimit: 30,",
  "new": "        retainedFileCountLimit: 336,",
  "marker": "retainedFileCountLimit: 336,"
 },
 {
  "file": "Backend\\PlantProcess.Api\\Program.cs",
  "anchor": "        restrictedToMinimumLevel: LogEventLevel.Verbose)",
  "new": "        restrictedToMinimumLevel: fileMinLevel)",
  "marker": "restrictedToMinimumLevel: fileMinLevel)"
 },
 {
  "file": "Backend\\PlantProcess.Api\\Program.cs",
  "anchor": "    .CreateLogger();",
  "new": "    .WriteTo.Logger(joblogSink => joblogSink\n        .Filter.ByIncludingOnly(logEvent => logEvent.Properties.ContainsKey(\"JobLog\"))\n        .WriteTo.File(\n            path: jobLogFilePath,\n            rollingInterval: RollingInterval.Hour,\n            retainedFileCountLimit: 336,\n            shared: true,\n            outputTemplate: \"[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] [{JobType}] {Message:lj}{NewLine}\",\n            restrictedToMinimumLevel: LogEventLevel.Information))\n    .CreateLogger();",
  "marker": "joblogSink"
 },
 {
  "file": "Backend\\PlantProcess.Api\\Program.cs",
  "anchor": "    builder.Services.AddScoped<IAuditLogService, AuditLogService>();",
  "new": "    builder.Services.AddScoped<IAuditLogService, AuditLogService>();\n    builder.Services.AddScoped<PlantProcess.Api.Observability.IJobLogService, PlantProcess.Api.Observability.JobLogService>();",
  "marker": "IJobLogService"
 },
 {
  "file": "Backend\\PlantProcess.Api\\Endpoints\\Admin\\TwoStageImportEndpoints.cs",
  "anchor": "        group.MapPost(\"/stage1/run\", RunStage1Async)",
  "new": "        group.MapPost(\"/stage1/run\", RunStage1Async)\n            .AddEndpointFilter(new PlantProcess.Api.Observability.JobLogEndpointFilter(\"Import-Stage1\"))",
  "marker": "JobLogEndpointFilter(\"Import-Stage1\""
 },
 {
  "file": "Backend\\PlantProcess.Api\\Endpoints\\Admin\\TwoStageImportEndpoints.cs",
  "anchor": "        group.MapPost(\"/stage2/run\", RunStage2Async)",
  "new": "        group.MapPost(\"/stage2/run\", RunStage2Async)\n            .AddEndpointFilter(new PlantProcess.Api.Observability.JobLogEndpointFilter(\"Import-Stage2\"))",
  "marker": "JobLogEndpointFilter(\"Import-Stage2\""
 },
 {
  "file": "Backend\\PlantProcess.Api\\Endpoints\\Admin\\TwoStageImportEndpoints.cs",
  "anchor": "        group.MapPost(\"/run-full-cycle\", RunFullCycleAsync)",
  "new": "        group.MapPost(\"/run-full-cycle\", RunFullCycleAsync)\n            .AddEndpointFilter(new PlantProcess.Api.Observability.JobLogEndpointFilter(\"Import-FullCycle\"))",
  "marker": "JobLogEndpointFilter(\"Import-FullCycle\""
 },
 {
  "file": "Backend\\PlantProcess.Api\\Endpoints\\Admin\\ConnectorAdminEndpoints.cs",
  "anchor": "        group.MapPost(\"/connection-profiles/{id:guid}/test\", TestConnectionProfileAsync)",
  "new": "        group.MapPost(\"/connection-profiles/{id:guid}/test\", TestConnectionProfileAsync)\n            .AddEndpointFilter(new PlantProcess.Api.Observability.JobLogEndpointFilter(\"ConnectorTest\"))",
  "marker": "JobLogEndpointFilter(\"ConnectorTest\""
 },
 {
  "file": "Backend\\PlantProcess.Api\\Endpoints\\Analytics\\AdvancedResultsEndpoints.cs",
  "anchor": "engine_key IN ('dotnet-analytics-core-v1','managed-stat-v1')",
  "new": "engine_key IN ('dotnet-analytics-core-v1','managed-stat-v1','ppiql-deterministic-core-v1')",
  "marker": "'ppiql-deterministic-core-v1'"
 },
 {
  "file": "Backend\\PlantProcess.Api\\Endpoints\\Admin\\AdminEndpoints.cs",
  "anchor": "        group.MapGet(\"/site-identity\", GetSiteIdentityAsync)",
  "new": "        group.MapGet(\"/job-logs\", GetJobLogsAsync)\n            .WithSummary(\"Get job event logs\")\n            .WithDescription(\"Customer-oriented job event stream with jobType / jobName / severity / day filters (paged, max 500 per page).\");\n\n        group.MapGet(\"/site-identity\", GetSiteIdentityAsync)",
  "marker": "\"/job-logs\""
 },
 {
  "file": "Backend\\PlantProcess.Api\\Endpoints\\Admin\\AdminEndpoints.cs",
  "anchor": "    private static async Task<IResult> GetSiteIdentityAsync(",
  "new": "    private static async Task<IResult> GetJobLogsAsync(\n        string? jobType,\n        string? jobName,\n        string? severity,\n        DateOnly? day,\n        int? page,\n        PlantProcessDbContext dbContext,\n        CancellationToken cancellationToken)\n    {\n        var pageSize = 500;\n        var pageIndex = Math.Max(0, (page ?? 1) - 1);\n\n        var sql =\n            \"SELECT id, occurred_at_utc, job_type, job_name, run_id, severity, message, context::text AS context, site_code \" +\n            \"FROM public.job_log WHERE 1 = 1\";\n        var connection = dbContext.Database.GetDbConnection();\n        if (connection.State != System.Data.ConnectionState.Open)\n        {\n            await connection.OpenAsync(cancellationToken);\n        }\n\n        await using var command = connection.CreateCommand();\n        void AddParam(string name, object value)\n        {\n            var p = command.CreateParameter();\n            p.ParameterName = name;\n            p.Value = value;\n            command.Parameters.Add(p);\n        }\n\n        if (!string.IsNullOrWhiteSpace(jobType)) { sql += \" AND job_type = @jobType\"; AddParam(\"jobType\", jobType); }\n        if (!string.IsNullOrWhiteSpace(jobName)) { sql += \" AND job_name ILIKE @jobName\"; AddParam(\"jobName\", \"%\" + jobName + \"%\"); }\n        if (!string.IsNullOrWhiteSpace(severity)) { sql += \" AND severity = @severity\"; AddParam(\"severity\", severity); }\n        if (day.HasValue)\n        {\n            sql += \" AND occurred_at_utc >= @dayStart AND occurred_at_utc < @dayEnd\";\n            var start = day.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);\n            AddParam(\"dayStart\", start);\n            AddParam(\"dayEnd\", start.AddDays(1));\n        }\n\n        sql += \" ORDER BY occurred_at_utc DESC LIMIT \" + pageSize + \" OFFSET \" + (pageIndex * pageSize);\n        command.CommandText = sql;\n\n        var entries = new List<JobLogEntryResponse>();\n        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))\n        {\n            while (await reader.ReadAsync(cancellationToken))\n            {\n                entries.Add(new JobLogEntryResponse(\n                    reader.GetGuid(0),\n                    reader.GetDateTime(1),\n                    reader.GetString(2),\n                    reader.GetString(3),\n                    reader.IsDBNull(4) ? null : reader.GetGuid(4),\n                    reader.GetString(5),\n                    reader.GetString(6),\n                    reader.GetString(7),\n                    reader.IsDBNull(8) ? null : reader.GetString(8)));\n            }\n        }\n\n        return Results.Ok(new { page = pageIndex + 1, pageSize, entries });\n    }\n\n    private sealed record JobLogEntryResponse(\n        Guid Id,\n        DateTime OccurredAtUtc,\n        string JobType,\n        string JobName,\n        Guid? RunId,\n        string Severity,\n        string Message,\n        string Context,\n        string? SiteCode);\n\n    private static async Task<IResult> GetSiteIdentityAsync(",
  "marker": "private static async Task<IResult> GetJobLogsAsync("
 }
]
'@
$edits = $editsJson | ConvertFrom-Json
foreach ($e in $edits) {
    $p = Join-Path $RepoRoot $e.file
    $raw = [System.IO.File]::ReadAllText($p)
    $isCrlf = $raw.Contains("`r`n")
    $t = $raw.Replace("`r", "")
    if ($t.Contains($e.marker)) {
        Write-Host ('      already applied - skipped: ' + $e.file.Split('\')[-1] + ' :: ' + $e.marker)
        continue
    }
    $c = ([regex]::Matches($t, [regex]::Escape($e.anchor))).Count
    $expected = 1
    if ($e.anchor.Contains('engine_key IN')) { $expected = $c }
    if ($c -lt 1) { throw ('anchor missing in ' + $e.file + ': ' + $e.anchor.Substring(0, [Math]::Min(70, $e.anchor.Length))) }
    if ($expected -eq 1 -and $c -ne 1) { throw ('anchor found ' + $c + ' times in ' + $e.file + ' - refusing: ' + $e.anchor.Substring(0, [Math]::Min(70, $e.anchor.Length))) }
    $t = $t.Replace($e.anchor, ($e.new -replace "`r", ""))
    if ($isCrlf) { $t = $t -replace "`n", "`r`n" }
    [System.IO.File]::WriteAllText($p, $t, $enc)
    Write-Host ('      edited ' + $e.file.Split('\')[-1] + ' (' + $c + ' site(s))')
}

# ---------------------------------------------------------------- [3/5] new files + SQL
Write-Host '[3/5] New files + job_log schema'
$svcPath = Join-Path $RepoRoot 'Backend\PlantProcess.Api\Observability\JobLogService.cs'
New-Item -ItemType Directory -Path (Split-Path $svcPath) -Force | Out-Null
$svcBody = @'
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PlantProcess.Infrastructure.Persistence;
using Serilog;

namespace PlantProcess.Api.Observability;

/// <summary>
/// Customer-oriented job event log (V1-45): one write lands the event in the job_log
/// table (HMI log panel + admin API) AND emits a Serilog event carrying JobLog=true,
/// which the filtered sub-logger mirrors into logs/joblog_yyyyMMddHH.log.
/// </summary>
public interface IJobLogService
{
    Task WriteAsync(
        string jobType,
        string jobName,
        Guid? runId,
        string severity,
        string message,
        object? context,
        CancellationToken cancellationToken);
}

public sealed class JobLogService : IJobLogService
{
    private readonly PlantProcessDbContext _dbContext;

    public JobLogService(PlantProcessDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task WriteAsync(
        string jobType,
        string jobName,
        Guid? runId,
        string severity,
        string message,
        object? context,
        CancellationToken cancellationToken)
    {
        var contextJson = context is null ? "{}" : JsonSerializer.Serialize(context);

        await _dbContext.Database.ExecuteSqlRawAsync(
            "INSERT INTO public.job_log (job_type, job_name, run_id, severity, message, context) " +
            "VALUES ({0}, {1}, {2}, {3}, {4}, {5}::jsonb)",
            new object?[] { jobType, jobName, runId, severity, message, contextJson },
            cancellationToken);

        var level = severity switch
        {
            "Error" => Serilog.Events.LogEventLevel.Error,
            "Warning" => Serilog.Events.LogEventLevel.Warning,
            _ => Serilog.Events.LogEventLevel.Information,
        };

        Log.ForContext("JobLog", true)
            .ForContext("JobType", jobType)
            .ForContext("JobName", jobName)
            .ForContext("JobRunId", runId)
            .Write(level, "{JobType} {JobName}: {JobMessage}", jobType, jobName, message);
    }
}

'@
[System.IO.File]::WriteAllText($svcPath, $svcBody, $enc)
$filterPath = Join-Path $RepoRoot 'Backend\PlantProcess.Api\Observability\JobLogEndpointFilter.cs'
$filterBody = @'
using System.Diagnostics;
using System.Reflection;

namespace PlantProcess.Api.Observability;

/// <summary>
/// Endpoint filter that turns any job-style endpoint into a job_log event stream:
/// Started before execution, Completed with duration on success, Failed with the
/// error message on exception (rethrown - behavior is never altered).
/// </summary>
public sealed class JobLogEndpointFilter : IEndpointFilter
{
    private readonly string _jobType;

    public JobLogEndpointFilter(string jobType)
    {
        _jobType = jobType;
    }

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var services = context.HttpContext.RequestServices;
        var jobLog = services.GetService<IJobLogService>();
        var ct = context.HttpContext.RequestAborted;

        var jobName = _jobType;
        foreach (var arg in context.Arguments)
        {
            var prop = arg?.GetType().GetProperty("RequestedBy", BindingFlags.Public | BindingFlags.Instance);
            var requestedBy = prop?.GetValue(arg) as string;
            if (!string.IsNullOrWhiteSpace(requestedBy))
            {
                jobName = _jobType + " (" + requestedBy + ")";
                break;
            }
        }

        if (jobLog is not null)
        {
            await jobLog.WriteAsync(_jobType, jobName, null, "Info", "Started", new { path = context.HttpContext.Request.Path.Value }, ct);
        }

        var sw = Stopwatch.StartNew();
        try
        {
            var result = await next(context);
            sw.Stop();
            if (jobLog is not null)
            {
                await jobLog.WriteAsync(_jobType, jobName, null, "Info", "Completed in " + sw.ElapsedMilliseconds + " ms", new { durationMs = sw.ElapsedMilliseconds }, ct);
            }
            return result;
        }
        catch (Exception ex)
        {
            sw.Stop();
            if (jobLog is not null)
            {
                await jobLog.WriteAsync(_jobType, jobName, null, "Error", "Failed after " + sw.ElapsedMilliseconds + " ms: " + ex.Message, new { durationMs = sw.ElapsedMilliseconds, error = ex.GetType().Name }, CancellationToken.None);
            }
            throw;
        }
    }
}

'@
[System.IO.File]::WriteAllText($filterPath, $filterBody, $enc)
Write-Host '      wrote Observability\JobLogService.cs + JobLogEndpointFilter.cs'

$sqlPath = Join-Path $RepoRoot 'Backend\database\scripts\252_job_event_log.sql'
$sqlBody = @'
-- 252_job_event_log.sql
-- Customer-oriented job event log (V1-45). Every operational job writes Started /
-- Completed / Failed events here; the admin API + HMI log panel read it; a filtered
-- Serilog sub-logger mirrors the stream to logs/joblog_yyyyMMddHH.log hourly files.
CREATE TABLE IF NOT EXISTS public.job_log
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    occurred_at_utc timestamptz NOT NULL DEFAULT now(),
    job_type text NOT NULL,
    job_name text NOT NULL,
    run_id uuid NULL,
    severity text NOT NULL CHECK (severity IN ('Info', 'Warning', 'Error')),
    message text NOT NULL,
    context jsonb NOT NULL DEFAULT '{}'::jsonb,
    site_code text NULL
);

CREATE INDEX IF NOT EXISTS ix_job_log_occurred
    ON public.job_log (occurred_at_utc DESC);

CREATE INDEX IF NOT EXISTS ix_job_log_type_severity
    ON public.job_log (job_type, severity);

'@
[System.IO.File]::WriteAllText($sqlPath, $sqlBody, $enc)
$PgUser = if ($env:PPIQ_PG_USER) { $env:PPIQ_PG_USER } else { 'ppiq_dev' }
$PgPass = if ($env:PPIQ_PG_PASS) { $env:PPIQ_PG_PASS } else { 'ppiq_dev_local_only' }
$psql = (Get-Command psql -ErrorAction SilentlyContinue).Source
if (-not $psql) { $psql = (Get-ChildItem 'C:\Program Files\PostgreSQL\*\bin\psql.exe' | Sort-Object FullName -Descending | Select-Object -First 1).FullName }
$env:PGPASSWORD = $PgPass
& $psql -h localhost -p 5432 -U $PgUser -d ppiq_app -v ON_ERROR_STOP=1 -q -f $sqlPath
if ($LASTEXITCODE -ne 0) { throw 'job_log schema apply failed' }
Write-Host '      script 252 committed to repo AND applied to the live DB'

$toolPath = Join-Path $RepoRoot 'tools\reset-app-database.ps1'
$toolBody = @'
# tools\reset-app-database.ps1  (V1-47)
# Fresh-start: empties ppiq_app to the exact state a customer sees on DAY ONE.
# LOCAL ONLY. Admin Golden Rule preserved: FirstRunProvisioning (at next API start)
# creates ONLY the permanent sysadmin; tenant admins remain a manual commissioning step.
# Distinct from tools\reset-emulation-sources.ps1 (which resets the SOURCE fleet).
param(
    [string]$RepoRoot = 'C:\Workspace\PlantProcess-IQ',
    [string]$PgUser = $(if ($env:PPIQ_PG_USER) { $env:PPIQ_PG_USER } else { 'ppiq_dev' }),
    [string]$PgPass = $(if ($env:PPIQ_PG_PASS) { $env:PPIQ_PG_PASS } else { 'ppiq_dev_local_only' }),
    [string]$PgDb = 'ppiq_app'
)
$ErrorActionPreference = 'Stop'
Write-Host 'FRESH-START: this DROPS and recreates the LOCAL application database (' -NoNewline
Write-Host $PgDb -ForegroundColor Red -NoNewline
Write-Host '). Source fleet, code, and backups are untouched.'
$confirm = Read-Host 'Type RESET to proceed'
if ($confirm -cne 'RESET') { throw 'aborted' }

$psql = (Get-Command psql -ErrorAction SilentlyContinue).Source
if (-not $psql) { $psql = (Get-ChildItem 'C:\Program Files\PostgreSQL\*\bin\psql.exe' | Sort-Object FullName -Descending | Select-Object -First 1).FullName }
$env:PGPASSWORD = $PgPass

Write-Host '[1/4] Dropping + recreating the database'
& $psql -h localhost -p 5432 -U $PgUser -d postgres -v ON_ERROR_STOP=1 -c ('DROP DATABASE IF EXISTS ' + $PgDb + ' WITH (FORCE);')
if ($LASTEXITCODE -ne 0) { throw 'drop failed (is the API still running?)' }
& $psql -h localhost -p 5432 -U $PgUser -d postgres -v ON_ERROR_STOP=1 -c ('CREATE DATABASE ' + $PgDb + ';')
if ($LASTEXITCODE -ne 0) { throw 'create failed' }

Write-Host '[2/4] EF Core migrations'
Push-Location (Join-Path $RepoRoot 'Backend')
try {
    dotnet ef database update --project PlantProcess.Infrastructure --startup-project PlantProcess.Api
    if ($LASTEXITCODE -ne 0) { throw 'dotnet ef database update failed' }
} finally { Pop-Location }

Write-Host '[3/4] Numbered SQL scripts (in order)'
$scripts = Get-ChildItem (Join-Path $RepoRoot 'Backend\database\scripts\*.sql') | Sort-Object Name
foreach ($s in $scripts) {
    Write-Host ('      ' + $s.Name)
    & $psql -h localhost -p 5432 -U $PgUser -d $PgDb -v ON_ERROR_STOP=1 -q -f $s.FullName
    if ($LASTEXITCODE -ne 0) { throw ('script failed: ' + $s.Name) }
}

Write-Host '[4/4] Day-one verification'
function Count([string]$t) { (& $psql -h localhost -p 5432 -U $PgUser -d $PgDb -t -A -c ('SELECT count(*) FROM ' + $t + ';')) }
foreach ($t in @('material_units', 'source_table_dump_registry', 'ml_correlation_compute_runs', 'job_log')) {
    Write-Host ('      ' + $t + ' = ' + (Count $t))
}
Write-Host ''
Write-Host 'DAY-ONE STATE READY.' -ForegroundColor Green
Write-Host 'Next: start the API (.\scripts\run\start-api.ps1 -Profile local) - FirstRunProvisioning'
Write-Host 'creates ONLY the permanent sysadmin; log in and walk the journey from an empty plant.'

'@
[System.IO.File]::WriteAllText($toolPath, $toolBody, $enc)
Write-Host '      wrote tools\reset-app-database.ps1 (V1-47)'

# ---------------------------------------------------------------- [4/5] gates
Write-Host '[4/5] Gates'
$api = Get-Process -Name 'PlantProcess.Api' -ErrorAction SilentlyContinue
if ($api) { $api | Stop-Process -Force; Start-Sleep -Seconds 2; Write-Host '      stopped running API' }
Push-Location (Join-Path $RepoRoot 'Backend')
try {
    dotnet build --nologo
    if ($LASTEXITCODE -ne 0) { throw 'dotnet build FAILED' }
    dotnet test --nologo
    if ($LASTEXITCODE -ne 0) { throw 'dotnet test FAILED' }
} finally { Pop-Location }

# ---------------------------------------------------------------- [5/5] acceptance guide
Write-Host '[5/5] GREEN. Acceptance (do now, ~3 min):'
Write-Host '  1. Start the API; startup log should show LogPath=...logs\systemlog_.log and the reaper line.'
Write-Host '  2. Run one Stage-1 from the HMI or: the walk prover Stage-1 step.'
Write-Host '  3. SELECT job_type, severity, message FROM job_log ORDER BY occurred_at_utc DESC LIMIT 6;'
Write-Host '     -> Started + Completed rows for Import-Stage1.'
Write-Host '  4. dir Backend\PlantProcess.Api\bin\Debug\net9.0\logs  -> systemlog_yyyyMMddHH.log AND joblog_yyyyMMddHH.log'
Write-Host '  5. GET /admin/job-logs?severity=Info  (Bearer token) -> the same events, paged.'
Write-Host '  6. CorrelationPage / Advanced results now resolve the deterministic-core run: the 13'
Write-Host '     findings render with the superheat driver on top (V1-42 HMI half).'
if ($env:PPIQ_COMMIT -eq '1') {
    Push-Location $RepoRoot
    try {
        git add Backend/PlantProcess.Api/Program.cs Backend/PlantProcess.Api/Observability Backend/PlantProcess.Api/Endpoints/Admin/TwoStageImportEndpoints.cs Backend/PlantProcess.Api/Endpoints/Admin/ConnectorAdminEndpoints.cs Backend/PlantProcess.Api/Endpoints/Admin/AdminEndpoints.cs Backend/PlantProcess.Api/Endpoints/Analytics/AdvancedResultsEndpoints.cs Backend/database/scripts/252_job_event_log.sql tools/reset-app-database.ps1
        $msgFile = Join-Path $env:TEMP ('ppiq-night-logging-' + $stamp + '.txt')
        $msg = @(
            'Observability: hourly system log, customer job-event log, results-endpoint engine fix, day-one reset tool',
            '',
            '- Serilog: systemlog_yyyyMMddHH.log hourly (retention 336), file level Information+ outside',
            '  Development; joblog_yyyyMMddHH.log hourly via a JobLog-filtered sub-logger.',
            '- job_log table (script 252) + JobLogService (DB row + mirrored Serilog event) +',
            '  JobLogEndpointFilter on Stage-1/Stage-2/full-cycle/connector-test (Started/Completed/',
            '  Failed, behavior untouched) + GET /admin/job-logs with type/name/severity/day filters.',
            '- Advanced results run resolution includes ppiql-deterministic-core-v1 so the HMI renders',
            '  the deterministic-core findings.',
            '- tools/reset-app-database.ps1: guarded local fresh-start (drop -> EF -> numbered SQL),',
            '  Admin Golden Rule preserved.'
        )
        [System.IO.File]::WriteAllText($msgFile, ($msg -join "`n"), $enc)
        git commit -F $msgFile
        Write-Host 'Committed.'
    } finally { Pop-Location }
} else {
    Write-Host 'Commit skipped. $env:PPIQ_COMMIT=''1'' and re-run to commit (idempotent).'
}
}
