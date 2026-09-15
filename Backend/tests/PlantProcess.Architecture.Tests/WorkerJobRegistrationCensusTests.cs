// T-106 B2.3c acceptance 13 and 14, as a permanent gate rather than a one-off report.
//
// Two of the four jobs this product ran on a timer had no JobDefinition at all until
// B2.3b: nothing could schedule, disable, monitor or audit them, and no test would have
// noticed. This reads the two sources and holds the line. It is a source-level gate for
// the same reason the migration-order gate is: the correspondence must be true in the
// repository, not only in a database somebody remembered to build.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace PlantProcess.Architecture.Tests;

[Trait("Gate", "WorkerJobRegistration")]
[Trait("BacklogTask", "T-106")]
public sealed class WorkerJobRegistrationCensusTests
{
    private const string WorkerRelativePath = "Backend/PlantProcess.Workers/Worker.cs";

    private const string RegistrationRelativePath =
        "Backend/PlantProcess.Application/Integration/Services/Jobs/JobRegistrationService.cs";

    /// <summary>
    /// The execution family each dispatched job belongs to, frozen by CENTRAL ruling:
    /// the import queue maps staged batches into canonical records and is therefore a
    /// CanonicalRefresh, while the delta import reads raw source rows and is a DbLinkImport.
    /// </summary>
    private static readonly Dictionary<string, string> ExpectedFamilies = new(StringComparer.Ordinal)
    {
        ["SYSTEM_IMPORT_QUEUE_PROCESSOR"] = "CanonicalRefresh",
        ["SYSTEM_DELTA_IMPORT_JOB"] = "DbLinkImport",
        ["SYSTEM_DATA_QUALITY_SCAN"] = "DataQualityScan",
        ["SYSTEM_RISK_SCORING"] = "RiskScoring"
    };

    [Fact]
    public void Every_job_code_the_worker_dispatches_is_registered()
    {
        var registration = ReadRepositoryFile(RegistrationRelativePath);

        foreach (var code in DispatchedJobCodes())
        {
            Assert.True(
                registration.Contains("JobCode: \"" + code + "\"", StringComparison.Ordinal),
                "The Worker dispatches " + code + " but the canonical registration catalogue does not declare it. "
                    + "A job nothing can schedule, disable or audit is not a governed job.");
        }
    }

    [Fact]
    public void Every_dispatched_job_is_registered_in_its_established_execution_family()
    {
        var registration = ReadRepositoryFile(RegistrationRelativePath);

        foreach (var code in DispatchedJobCodes())
        {
            Assert.True(
                ExpectedFamilies.ContainsKey(code),
                "The Worker dispatches " + code + ", which has no frozen execution family. "
                    + "Add it to the ruling and to this gate rather than letting it run unclassified.");

            var declared = DeclaredFamily(registration, code);

            Assert.Equal(ExpectedFamilies[code], declared);
        }
    }

    [Fact]
    public void The_census_reads_a_worker_that_actually_dispatches_something()
    {
        // A gate that silently matches nothing is worse than no gate. If the Worker stops
        // naming job codes, this fails rather than passing on an empty set.
        Assert.NotEmpty(DispatchedJobCodes());
    }

    private static IReadOnlyList<string> DispatchedJobCodes()
    {
        var worker = ReadRepositoryFile(WorkerRelativePath);
        var codes = new List<string>();

        foreach (Match match in Regex.Matches(worker, "\"(SYSTEM_[A-Z0-9_]+)\""))
        {
            var code = match.Groups[1].Value;
            if (!codes.Contains(code, StringComparer.Ordinal))
            {
                codes.Add(code);
            }
        }

        return codes;
    }

    private static string DeclaredFamily(string registration, string jobCode)
    {
        var index = registration.IndexOf("JobCode: \"" + jobCode + "\"", StringComparison.Ordinal);
        Assert.True(index >= 0, "No registration entry declares " + jobCode + ".");

        var window = registration.Substring(index, Math.Min(900, registration.Length - index));
        var match = Regex.Match(window, @"JobType:\s*JobDefinitionType\.(\w+)");

        Assert.True(match.Success, "The registration entry for " + jobCode + " declares no JobType.");
        return match.Groups[1].Value;
    }

    private static string ReadRepositoryFile(string relativePath)
    {
        var root = FindRepositoryRoot();
        var full = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));

        Assert.True(File.Exists(full), "Expected source file is missing: " + relativePath);
        return File.ReadAllText(full);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, ".git")) ||
                Directory.Exists(Path.Combine(directory.FullName, "Backend")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root not found from " + AppContext.BaseDirectory);
    }
}
