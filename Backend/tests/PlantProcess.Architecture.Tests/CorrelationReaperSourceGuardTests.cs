using System.Text.RegularExpressions;
using Xunit;

namespace PlantProcess.Architecture.Tests;

/// <summary>
/// V1-41 / V1-42 source guards. These lock in the correlation run-to-result fix and the
/// stuck-run reaper at the source level so neither can silently regress: the reaper service
/// must exist and target Running rows past a max age; the learning function must NOT write
/// the phantom columns that caused the 347-zombie defect; and both silent WHEN OTHERS THEN
/// NULL swallows in that function must be gone.
/// </summary>
public sealed class CorrelationReaperSourceGuardTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "Backend")))
        {
            dir = dir.Parent;
        }
        Assert.True(dir is not null, "Could not locate repo root (Backend folder).");
        return dir!.FullName;
    }

    [Fact]
    public void Reaper_hosted_service_exists_and_targets_overage_running_runs()
    {
        var path = Path.Combine(RepoRoot(), "Backend", "PlantProcess.Api", "Hosting", "ComputeRunReaperHostedService.cs");
        Assert.True(File.Exists(path), "V1-41: ComputeRunReaperHostedService.cs must exist.");
        var src = File.ReadAllText(path);

        Assert.Contains("BackgroundService", src);
        Assert.Contains("ml_correlation_compute_runs", src);
        Assert.Matches(new Regex(@"status\s*=\s*'Failed'", RegexOptions.IgnoreCase), src);
        Assert.Contains("started_at_utc <", src);
        Assert.Contains("timeout", src);
    }

    [Fact]
    public void Reaper_is_registered_in_program()
    {
        var program = File.ReadAllText(Path.Combine(RepoRoot(), "Backend", "PlantProcess.Api", "Program.cs"));
        Assert.Contains("AddHostedService<PlantProcess.Api.Hosting.ComputeRunReaperHostedService>", program);
    }

    [Fact]
    public void Learning_function_no_longer_writes_phantom_columns_or_swallows_errors()
    {
        var sql = File.ReadAllText(Path.Combine(RepoRoot(), "Backend", "database", "scripts", "204_phase04_phase05_ml_learning_core.sql"));

        // Extract the compute-run completion UPDATE and prove it targets real columns only.
        var completion = new Regex(
            @"UPDATE\s+public\.ml_correlation_compute_runs\s+SET\s+status\s*=\s*'Completed'[\s\S]*?WHERE\s+id\s*=\s*v_compute_run_id;",
            RegexOptions.IgnoreCase);
        var match = completion.Match(sql);
        Assert.True(match.Success, "V1-42: compute-run completion UPDATE not found in the learning function.");
        Assert.DoesNotContain("finished_at_utc", match.Value);
        Assert.DoesNotContain("result_count", match.Value);
        Assert.Contains("completed_at_utc", match.Value);
        Assert.Contains("duration_ms", match.Value);

        // Neither swallow may remain inside the function body.
        var fn = new Regex(
            @"CREATE OR REPLACE FUNCTION public\.ppiq_ml_run_learning_job_v1[\s\S]*?\$\$;",
            RegexOptions.IgnoreCase).Match(sql);
        Assert.True(fn.Success, "learning function body not found.");
        Assert.DoesNotContain("WHEN OTHERS THEN\n            NULL;", fn.Value.Replace("\r", ""));
    }
}
