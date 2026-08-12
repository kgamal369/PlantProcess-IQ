using System.Diagnostics;
using System.Text;
using PlantProcess.ML.Runtime;

namespace PlantProcess.ML.Runtime.Tests;

/// <summary>
/// Locates the ML project and a usable interpreter. Fails loudly rather than
/// skipping, because a skipped test proves nothing about the boundary.
/// </summary>
public static class PythonEnvironment
{
    private static string? _interpreter;

    public static string MlRoot => FindMlRoot();

    public static string Interpreter => _interpreter ??= FindInterpreter();

    public static PythonRuntimeOptions Options() => new(
        Interpreter,
        MlRoot,
        new[] { Path.Combine(MlRoot, "src"), MlRoot });

    private static string FindMlRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "ML");
            if (Directory.Exists(Path.Combine(candidate, "src", "ppiq_ml", "runtime")))
                return candidate;
            directory = directory.Parent;
        }
        throw new InvalidOperationException(
            "The ML/ project was not found above the test output directory. "
            + "T-168 Pack 1 must be present for the end-to-end protocol tests to run.");
    }

    private static string FindInterpreter()
    {
        foreach (var candidate in new[] { "python", "python3", "py" })
        {
            try
            {
                var info = new ProcessStartInfo(candidate)
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                info.ArgumentList.Add("--version");
                using var process = Process.Start(info);
                if (process is null) continue;
                var text = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
                process.WaitForExit(10000);
                if (process.ExitCode != 0) continue;

                var match = System.Text.RegularExpressions.Regex.Match(text, @"Python 3\.(\d+)");
                if (match.Success && int.Parse(match.Groups[1].Value) >= 11) return candidate;
            }
            catch (System.ComponentModel.Win32Exception) { }
            catch (InvalidOperationException) { }
        }

        throw new InvalidOperationException(
            "Python 3.11 or newer was not found on PATH. The T-168 end-to-end protocol "
            + "tests execute the real Python runtime and cannot be proven without it. "
            + "They fail rather than skip, because a skipped test proves nothing.");
    }

    /// <summary>Invokes the CLI directly, for cases that bypass the runner.</summary>
    public static int InvokeCli(string jobSpecPath, string handlerReference)
    {
        var info = new ProcessStartInfo(Interpreter)
        {
            WorkingDirectory = MlRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        info.ArgumentList.Add("-m");
        info.ArgumentList.Add("ppiq_ml.runtime.cli");
        info.ArgumentList.Add("--job-spec");
        info.ArgumentList.Add(jobSpecPath);
        info.ArgumentList.Add("--handler");
        info.ArgumentList.Add(handlerReference);
        info.Environment["PYTHONPATH"] = string.Join(Path.PathSeparator,
            new[] { Path.Combine(MlRoot, "src"), MlRoot });
        info.Environment["PYTHONDONTWRITEBYTECODE"] = "1";

        using var process = Process.Start(info)
            ?? throw new InvalidOperationException("Failed to start the Python interpreter.");
        _ = process.StandardOutput.ReadToEnd();
        _ = process.StandardError.ReadToEnd();
        process.WaitForExit(60000);
        return process.ExitCode;
    }
}
