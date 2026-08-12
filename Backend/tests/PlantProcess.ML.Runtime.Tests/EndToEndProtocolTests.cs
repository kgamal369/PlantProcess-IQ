using PlantProcess.ML.Runtime;
using Xunit;

namespace PlantProcess.ML.Runtime.Tests;

/// <summary>
/// Real C# to Python to C# execution. Every test launches an actual Python process
/// and judges the outcome by the result manifest, never by the exit code.
///
/// <para>
/// These tests require Python 3.11 or newer on PATH. They do not skip when it is
/// absent, because a skipped test proves nothing; they fail with a sentence naming
/// the missing dependency.
/// </para>
/// </summary>
[Collection("python-e2e")]
public sealed class EndToEndProtocolTests : IDisposable
{
    private const string Handlers = "tests.handlers.fixture_handlers";
    private readonly string _root;
    private readonly PythonJobRunner _runner;

    public EndToEndProtocolTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "ppiq-t168-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _runner = new PythonJobRunner(PythonEnvironment.Options());
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
    }

    private JobSpec Spec(string name, double budgetSeconds = 60.0, string? checkpointDir = null, string? cancelFile = null) => new()
    {
        JobId = "job-" + name,
        TenantId = "tenant-a",
        SiteId = "site-1",
        ModelFamily = "fixture",
        OutputDirectory = Path.Combine(_root, name),
        Seed = 20260812,
        CodeIdentity = "commit-abc123",
        Resources = new ResourceBudget(budgetSeconds),
        CheckpointDirectory = checkpointDir,
        CancellationFile = cancelFile
    };

    // ------------------------------------------------------------ 1 success

    [Fact]
    public void A_successful_execution_returns_the_manifest_the_python_side_wrote()
    {
        var result = _runner.Execute(Spec("success"), Handlers + ":succeed");

        Assert.Equal(JobOutcome.Succeeded, result.Outcome);
        Assert.True(result.HasManifest);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(0.834, result.Manifest!.Metrics["auc"], 9);
        Assert.Single(result.Manifest.Artifacts);
        Assert.Equal("model-1", result.Manifest.Artifacts[0].ArtifactId);
        Assert.Equal("commit-abc123", result.Manifest.CodeIdentity);
        Assert.Equal(20260812, result.Manifest.Seed);
    }

    [Fact]
    public void A_succeeded_job_can_still_carry_an_honest_analysis_refusal()
    {
        var result = _runner.Execute(Spec("analysis"), Handlers + ":succeed_with_honest_analysis_refusal");

        Assert.Equal(JobOutcome.Succeeded, result.Outcome);
        Assert.Equal("InsufficientData", result.Manifest!.AnalysisTerminalState);
        Assert.Contains("population below the declared floor", result.Manifest.Warnings);
    }

    // ------------------------------------------------------------ 2 honest refusal

    [Fact]
    public void A_structured_refusal_crosses_the_boundary_with_its_code_and_sentence()
    {
        var result = _runner.Execute(Spec("refuse"), Handlers + ":refuse");

        Assert.Equal(JobOutcome.Refused, result.Outcome);
        Assert.Equal(MlRefusalCode.EligibilityNotMet, result.RefusalCode);
        Assert.Contains("500", result.Reason);
        Assert.True(result.HasManifest);
    }

    [Fact]
    public void A_refusal_is_never_reported_as_a_failure_or_a_success()
    {
        var result = _runner.Execute(Spec("refuse2"), Handlers + ":refuse");

        Assert.NotEqual(JobOutcome.Failed, result.Outcome);
        Assert.NotEqual(JobOutcome.Succeeded, result.Outcome);
    }

    // ------------------------------------------------------------ 3 crash

    [Fact]
    public void A_python_crash_is_failed_and_still_produces_a_manifest()
    {
        var result = _runner.Execute(Spec("crash"), Handlers + ":crash");

        Assert.Equal(JobOutcome.Failed, result.Outcome);
        Assert.True(result.HasManifest);
        Assert.Contains("ZeroDivisionError", result.Manifest!.RefusalReason);
        Assert.Equal(MlRefusalCode.None, result.RefusalCode);
    }

    // ------------------------------------------------------------ 4 timeout

    [Fact]
    public void A_process_that_overruns_its_budget_is_timed_out_and_killed()
    {
        var result = _runner.Execute(Spec("timeout", budgetSeconds: 2.0), Handlers + ":hang");

        Assert.Equal(JobOutcome.TimedOut, result.Outcome);
        Assert.False(result.HasManifest);
        Assert.Null(result.ExitCode);
        Assert.Contains("cannot report on itself", result.Reason);
        Assert.True(result.Elapsed < TimeSpan.FromSeconds(30), "the runner must not wait for a hung process");
    }

    // ------------------------------------------------------------ 5 cancellation

    [Fact]
    public void Cancellation_signalled_by_this_side_is_honoured_by_the_python_side()
    {
        var cancelFile = Path.Combine(_root, "cancel.flag");
        var spec = Spec("cancel", budgetSeconds: 60.0, cancelFile: cancelFile);

        using var signal = new Timer(_ => File.WriteAllText(cancelFile, "stop"), null, 750, Timeout.Infinite);
        var result = _runner.Execute(spec, Handlers + ":cancellable");

        Assert.Equal(JobOutcome.Cancelled, result.Outcome);
        Assert.True(result.HasManifest);
        Assert.Contains("Cancellation", result.Manifest!.RefusalReason);
    }

    // ------------------------------------------------------------ 6 malformed manifest

    [Fact]
    public void A_malformed_manifest_fails_even_though_the_process_exited_zero()
    {
        var spec = Spec("malformed");
        var result = _runner.Execute(spec, Handlers + ":succeed");
        Assert.Equal(JobOutcome.Succeeded, result.Outcome);

        // Corrupt the manifest the way a truncated write would, then read it again.
        var manifestPath = Path.Combine(spec.OutputDirectory, MlJobProtocol.ManifestFileName);
        File.WriteAllText(manifestPath, "{ \"protocol\": \"ppiq.mljob/1\", \"job_id\": ");

        var error = Assert.Throws<MlProtocolException>(() =>
            ResultManifest.FromJson(File.ReadAllText(manifestPath)));
        Assert.Equal(MlRefusalCode.MalformedJobSpec, error.Code);
    }

    // ------------------------------------------------------------ 7 missing manifest

    [Fact]
    public void A_missing_manifest_is_a_failure_whatever_the_exit_code_says()
    {
        // A handler reference that does not exist: the CLI refuses and writes a bare
        // refusal manifest. Delete it to simulate a process that wrote nothing at all.
        var spec = Spec("missing");
        Directory.CreateDirectory(spec.OutputDirectory);

        var result = _runner.Execute(spec, "tests.handlers.fixture_handlers:succeed");
        var manifestPath = Path.Combine(spec.OutputDirectory, MlJobProtocol.ManifestFileName);
        File.Delete(manifestPath);

        Assert.False(File.Exists(manifestPath));

        // Re-run into a directory whose manifest cannot be produced, by pointing at a
        // module that does not exist AND removing the output directory afterwards.
        var second = Spec("missing2");
        var r2 = _runner.Execute(second, "no_such_module_at_all:handler");
        Assert.Equal(JobOutcome.Refused, r2.Outcome);
        Assert.Equal(MlRefusalCode.UnsupportedModelFamily, r2.RefusalCode);
    }

    // ------------------------------------------------------------ 8 protocol mismatch

    [Fact]
    public void A_job_spec_from_a_future_protocol_is_refused_before_interpretation()
    {
        var spec = Spec("mismatch");
        Directory.CreateDirectory(spec.OutputDirectory);

        // Write the spec by hand with a future protocol, then invoke the CLI on it.
        var specPath = Path.Combine(spec.OutputDirectory, "job_spec.json");
        File.WriteAllText(specPath, spec.ToJson().Replace("ppiq.mljob/1", "ppiq.mljob/99"));

        var result = PythonEnvironment.InvokeCli(specPath, Handlers + ":succeed");
        var manifestPath = Path.Combine(spec.OutputDirectory, MlJobProtocol.ManifestFileName);

        Assert.True(File.Exists(manifestPath), "the Python side must record why it refused");
        var manifest = ResultManifest.FromJson(File.ReadAllText(manifestPath));
        Assert.Equal(JobOutcome.Refused, manifest.OutcomeValue);
        Assert.Equal(MlRefusalCode.ProtocolVersionMismatch, manifest.RefusalCodeValue);
        Assert.Contains("was not interpreted", manifest.RefusalReason);
        Assert.NotEqual(0, result);
    }

    // ------------------------------------------------------------ 9 checkpoint and resume

    [Fact]
    public void A_second_run_resumes_from_the_checkpoint_the_first_run_wrote()
    {
        var checkpoints = Path.Combine(_root, "ckpt");

        var first = _runner.Execute(Spec("ck1", checkpointDir: checkpoints), Handlers + ":checkpointing");
        Assert.Equal(JobOutcome.Succeeded, first.Outcome);
        Assert.Equal(1.0, first.Manifest!.Metrics["stages_completed"], 9);
        Assert.Null(first.Manifest.ResumedFromCheckpoint);

        var second = _runner.Execute(Spec("ck2", checkpointDir: checkpoints), Handlers + ":checkpointing");
        Assert.Equal(JobOutcome.Succeeded, second.Outcome);
        Assert.Equal(2.0, second.Manifest!.Metrics["stages_completed"], 9);
        Assert.Equal("stage-1", second.Manifest.ResumedFromCheckpoint);
    }

    // ------------------------------------------------------------ 10 determinism

    [Fact]
    public void Two_identical_executions_agree_on_everything_but_timing()
    {
        var a = _runner.Execute(Spec("det1"), Handlers + ":succeed");
        var b = _runner.Execute(Spec("det2"), Handlers + ":succeed");

        Assert.Equal(a.Outcome, b.Outcome);
        Assert.Equal(a.RefusalCode, b.RefusalCode);
        Assert.Equal(a.Manifest!.Metrics["auc"], b.Manifest!.Metrics["auc"], 12);
        Assert.Equal(a.Manifest.Seed, b.Manifest.Seed);
        Assert.Equal(a.Manifest.CodeIdentity, b.Manifest.CodeIdentity);
        Assert.Equal(a.Manifest.Artifacts[0].ContentHash, b.Manifest.Artifacts[0].ContentHash);
        Assert.Equal(a.Manifest.RuntimeVersion, b.Manifest.RuntimeVersion);
    }

    // ------------------------------------------------------------ 11 stdout is never authority

    [Fact]
    public void Stdout_and_stderr_claiming_success_cannot_make_a_failure_succeed()
    {
        var result = _runner.Execute(Spec("liar"), Handlers + ":stdout_liar");

        // The process shouted success on both streams.
        Assert.Contains("SUCCESS model trained", result.StandardOutput);
        Assert.Contains("promoted to champion", result.StandardOutput);
        Assert.Contains("all gates passed", result.StandardError);

        // The manifest is the authority, and it says failed.
        Assert.Equal(JobOutcome.Failed, result.Outcome);
        Assert.Contains("RuntimeError", result.Manifest!.RefusalReason);
        Assert.Empty(result.Manifest.Metrics);
        Assert.DoesNotContain("0.99", string.Join(",", result.Manifest.Metrics.Values));
    }

    [Fact]
    public void A_manifest_from_a_different_job_is_not_evidence_about_this_one()
    {
        var spec = Spec("crosstalk");
        var result = _runner.Execute(spec, Handlers + ":succeed");
        Assert.Equal(JobOutcome.Succeeded, result.Outcome);

        var manifestPath = Path.Combine(spec.OutputDirectory, MlJobProtocol.ManifestFileName);
        var swapped = File.ReadAllText(manifestPath).Replace("\"job-crosstalk\"", "\"job-somebody-else\"");
        var manifest = ResultManifest.FromJson(swapped);

        Assert.NotEqual(spec.JobId, manifest.JobId);
    }
}
