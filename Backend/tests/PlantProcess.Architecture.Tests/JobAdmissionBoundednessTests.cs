using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace PlantProcess.Architecture.Tests;

/// <summary>
/// T-107. The two things a unit test cannot see: that production admission and dispatch
/// source contains no unbounded fan-out, and that admission introduced no second authority.
///
/// The scan covers production source only. A test may legitimately use Task.WhenAll to
/// generate concurrent load; production dispatch may not use it to start unbounded work.
/// </summary>
[Trait("Gate", "JobAdmissionBoundedness")]
public sealed class JobAdmissionBoundednessTests
{
    private static readonly string[] ProductionPaths =
    {
        "Backend/PlantProcess.Application/Jobs/Admission",
        "Backend/PlantProcess.Application/Jobs/Scheduling"
    };

    private static readonly Regex BlockComment = new(@"/\*[\s\S]*?\*/", RegexOptions.Compiled);
    private static readonly Regex LineComment = new(@"(?<!:)//[^\r\n]*", RegexOptions.Compiled);

    internal static string StripComments(string text)
        => LineComment.Replace(BlockComment.Replace(text, string.Empty), string.Empty);

    /// <summary>Returns every unbounded fan-out found in executable text. Empty means clean.</summary>
    internal static IReadOnlyList<string> ScanForUnboundedFanOut(string sourceText)
    {
        string executable = StripComments(sourceText);
        var findings = new List<string>();

        var patterns = new Dictionary<string, Regex>(StringComparer.Ordinal)
        {
            ["Task." + "WhenAll"] = new Regex(@"Task\s*\.\s*WhenAll\s*\(", RegexOptions.None),
            ["Parallel." + "ForEach"] = new Regex(@"Parallel\s*\.\s*For(Each)?(Async)?\s*\(", RegexOptions.None),
            ["fire and forget Task." + "Run"] = new Regex(@"(?<![=\w])\s*Task\s*\.\s*Run\s*\(", RegexOptions.None)
        };

        foreach (KeyValuePair<string, Regex> pattern in patterns)
        {
            if (pattern.Value.IsMatch(executable))
            {
                findings.Add(pattern.Key);
            }
        }

        return findings;
    }

    /// <summary>
    /// The one named exemption, stated rather than hidden in a regex: the dispatcher's own
    /// drain awaits the set of tasks it already tracks, which is bounded by construction.
    /// A separate test proves that use is confined to the drain.
    /// </summary>
    private const string DrainExemptFile = "BoundedJobDispatch.cs";

    [Fact]
    public void Production_admission_and_dispatch_source_holds_no_unbounded_fan_out()
    {
        var offenders = new List<string>();

        foreach (string file in ProductionSourceFiles())
        {
            if (string.Equals(Path.GetFileName(file), DrainExemptFile, StringComparison.Ordinal))
            {
                IReadOnlyList<string> drainFindings = ScanForUnboundedFanOut(File.ReadAllText(file))
                    .Where(finding => !finding.Contains("WhenAll", StringComparison.Ordinal))
                    .ToArray();

                if (drainFindings.Count > 0)
                {
                    offenders.Add(Path.GetFileName(file) + ": " + string.Join(", ", drainFindings));
                }

                continue;
            }

            IReadOnlyList<string> findings = ScanForUnboundedFanOut(File.ReadAllText(file));
            if (findings.Count > 0)
            {
                offenders.Add(Path.GetFileName(file) + ": " + string.Join(", ", findings));
            }
        }

        Assert.True(
            offenders.Count == 0,
            "Dispatch must be bounded and supervised. Offending files: " + string.Join(" | ", offenders));
    }

    [Fact]
    public void The_dispatchers_only_awaited_set_is_the_one_it_already_tracks()
    {
        string dispatcher = File.ReadAllText(Path.Combine(
            RepoRoot(), "Backend", "PlantProcess.Application", "Jobs", "Admission", DrainExemptFile));

        string executable = StripComments(dispatcher);

        int drainIndex = executable.IndexOf("DrainAsync", StringComparison.Ordinal);
        Assert.True(drainIndex >= 0, "The dispatcher must expose a drain.");

        foreach (Match match in new Regex(@"Task\s*\.\s*WhenAll\s*\(").Matches(executable))
        {
            Assert.True(
                match.Index > drainIndex,
                "The only awaited set is the tracked in-flight set inside the drain.");
        }

        Assert.Contains("Task.WhenAll(pending)", executable, StringComparison.Ordinal);
    }

    [Fact]
    public void The_fan_out_scan_goes_red_on_unbounded_work_and_stays_green_on_a_comment()
    {
        string forbidden = "await Task." + "WhenAll(everything);";
        string commented = "// await Task." + "WhenAll(everything);";

        Assert.NotEmpty(ScanForUnboundedFanOut(forbidden));
        Assert.Empty(ScanForUnboundedFanOut(commented));
    }

    [Fact]
    public void Admission_introduced_no_second_scheduler_run_store_or_capability_authority()
    {
        var offenders = new List<string>();

        string[] forbiddenConcepts =
        {
            "JobRunHistory",
            "IJobRuntimeService",
            "IJobExecutionCapabilityAuthority",
            "IJobExecutorResolver",
            "ScheduleOccurrenceCalculator",
            "DbContext"
        };

        foreach (string file in Directory.EnumerateFiles(
            Path.Combine(RepoRoot(), "Backend", "PlantProcess.Application", "Jobs", "Admission"),
            "*.cs",
            SearchOption.AllDirectories))
        {
            string executable = StripComments(File.ReadAllText(file));

            foreach (string concept in forbiddenConcepts)
            {
                if (executable.Contains(concept, StringComparison.Ordinal))
                {
                    offenders.Add(Path.GetFileName(file) + ": " + concept);
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "Admission answers one question and owns no run history, no schedule and no capability truth. " +
            "Offenders: " + string.Join(" | ", offenders));
    }

    [Fact]
    public void Admission_keeps_no_durable_queue()
    {
        var offenders = new List<string>();
        string[] persistenceTokens = { "Npgsql", "EntityFrameworkCore", "SaveChanges", "job_admission" };

        foreach (string file in Directory.EnumerateFiles(
            Path.Combine(RepoRoot(), "Backend", "PlantProcess.Application", "Jobs", "Admission"),
            "*.cs",
            SearchOption.AllDirectories))
        {
            string executable = StripComments(File.ReadAllText(file));
            foreach (string token in persistenceTokens)
            {
                if (executable.Contains(token, StringComparison.Ordinal))
                {
                    offenders.Add(Path.GetFileName(file) + ": " + token);
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "Waiting is runtime state; the schedule authority stays the durable truth. Offenders: " +
            string.Join(" | ", offenders));
    }

    private static IEnumerable<string> ProductionSourceFiles()
    {
        foreach (string relative in ProductionPaths)
        {
            string directory = Path.Combine(RepoRoot(), relative.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(directory))
            {
                continue;
            }

            foreach (string file in Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
            {
                if (file.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
                    file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                {
                    continue;
                }

                yield return file;
            }
        }
    }

    internal static string RepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Jenkinsfile")) &&
                Directory.Exists(Path.Combine(current.FullName, "Backend")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("The repository root with a Jenkinsfile could not be found.");
    }
}
