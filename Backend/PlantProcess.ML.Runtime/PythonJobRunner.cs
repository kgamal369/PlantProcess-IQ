using System.Diagnostics;
using System.Text;

namespace PlantProcess.ML.Runtime;

/// <summary>Where the runner looks for the Python side and how long it waits.</summary>
public sealed record PythonRuntimeOptions(
    string Interpreter,
    string MlProjectRoot,
    IReadOnlyList<string> ExtraPythonPaths);

/// <summary>
/// What the caller learns from one execution. Outcome is decided by the MANIFEST.
/// Exit code, stdout and stderr are carried for diagnosis and are never authority.
/// </summary>
public sealed record JobExecutionResult(
    JobOutcome Outcome,
    MlRefusalCode RefusalCode,
    string Reason,
    ResultManifest? Manifest,
    int? ExitCode,
    string StandardOutput,
    string StandardError,
    TimeSpan Elapsed)
{
    public bool HasManifest => Manifest is not null;
}

/// <summary>
/// Executes the Python ML runtime as a child process and reads its result manifest.
///
/// <para>
/// One rule governs this class: <b>the manifest is the only authority</b>. A process
/// that exits zero and prints SUCCESS but writes no valid manifest has failed. A
/// process that is killed on timeout has timed out regardless of what it printed
/// before it died.
/// </para>
/// </summary>
public sealed class PythonJobRunner
{
    private readonly PythonRuntimeOptions _options;

    public PythonJobRunner(PythonRuntimeOptions options) => _options = options;

    public JobExecutionResult Execute(JobSpec spec, string handlerReference)
    {
        if (spec is null) throw new ArgumentNullException(nameof(spec));
        if (string.IsNullOrWhiteSpace(handlerReference)) throw new ArgumentException("A handler reference is required.", nameof(handlerReference));

        Directory.CreateDirectory(spec.OutputDirectory);
        var manifestPath = Path.Combine(spec.OutputDirectory, MlJobProtocol.ManifestFileName);
        if (File.Exists(manifestPath)) File.Delete(manifestPath);

        var specPath = Path.Combine(spec.OutputDirectory, "job_spec.json");
        File.WriteAllText(specPath, spec.ToJson(), new UTF8Encoding(false));

        var start = new ProcessStartInfo
        {
            FileName = _options.Interpreter,
            WorkingDirectory = _options.MlProjectRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        start.ArgumentList.Add("-m");
        start.ArgumentList.Add("ppiq_ml.runtime.cli");
        start.ArgumentList.Add("--job-spec");
        start.ArgumentList.Add(specPath);
        start.ArgumentList.Add("--handler");
        start.ArgumentList.Add(handlerReference);
        start.Environment["PYTHONPATH"] = string.Join(Path.PathSeparator, _options.ExtraPythonPaths);
        start.Environment["PYTHONDONTWRITEBYTECODE"] = "1";

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        var clock = Stopwatch.StartNew();

        using var process = new Process { StartInfo = start, EnableRaisingEvents = true };
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) stdout.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) stderr.AppendLine(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var budget = TimeSpan.FromSeconds(spec.Resources.MaxWallClockSeconds);
        var exitedInTime = process.WaitForExit((int)Math.Min(budget.TotalMilliseconds, int.MaxValue));

        if (!exitedInTime)
        {
            TryKill(process);
            clock.Stop();
            return new JobExecutionResult(
                JobOutcome.TimedOut, MlRefusalCode.None,
                $"The Python runtime exceeded its wall-clock budget of "
                + $"{spec.Resources.MaxWallClockSeconds:0.###} seconds and was terminated. "
                + "A terminated process cannot report on itself, so this side records the timeout.",
                null, null, stdout.ToString(), stderr.ToString(), clock.Elapsed);
        }

        process.WaitForExit();
        clock.Stop();
        var exitCode = process.ExitCode;

        // From here the exit code is diagnostic only.
        if (!File.Exists(manifestPath))
        {
            return new JobExecutionResult(
                JobOutcome.Failed, MlRefusalCode.None,
                $"The Python runtime exited with code {exitCode} and wrote no result manifest. "
                + "Console output is not authority, so the execution is recorded as failed "
                + "whatever it printed.",
                null, exitCode, stdout.ToString(), stderr.ToString(), clock.Elapsed);
        }

        string text;
        try
        {
            text = File.ReadAllText(manifestPath);
        }
        catch (IOException exception)
        {
            return new JobExecutionResult(
                JobOutcome.Failed, MlRefusalCode.None,
                $"The result manifest could not be read: {exception.Message}",
                null, exitCode, stdout.ToString(), stderr.ToString(), clock.Elapsed);
        }

        ResultManifest manifest;
        try
        {
            manifest = ResultManifest.FromJson(text);
            manifest.ValidateRefusalConsistency();
        }
        catch (MlProtocolException protocolError)
        {
            return new JobExecutionResult(
                JobOutcome.Failed, protocolError.Code,
                "The result manifest is present but not usable: " + protocolError.Message,
                null, exitCode, stdout.ToString(), stderr.ToString(), clock.Elapsed);
        }

        if (!string.Equals(manifest.JobId, spec.JobId, StringComparison.Ordinal))
        {
            return new JobExecutionResult(
                JobOutcome.Failed, MlRefusalCode.MalformedJobSpec,
                $"The manifest reports job '{manifest.JobId}' but this execution was "
                + $"'{spec.JobId}'. A manifest from a different job is not evidence about this one.",
                null, exitCode, stdout.ToString(), stderr.ToString(), clock.Elapsed);
        }

        return new JobExecutionResult(
            manifest.OutcomeValue, manifest.RefusalCodeValue, manifest.RefusalReason,
            manifest, exitCode, stdout.ToString(), stderr.ToString(), clock.Elapsed);
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            process.WaitForExit(5000);
        }
        catch (InvalidOperationException) { }
        catch (NotSupportedException) { }
    }
}
