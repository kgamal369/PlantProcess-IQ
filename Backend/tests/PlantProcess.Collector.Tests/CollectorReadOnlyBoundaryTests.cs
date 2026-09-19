using System.Reflection;
using System.Text.RegularExpressions;
using PlantProcess.Collector.Host;
using PlantProcess.Collector.OpcUa;

namespace PlantProcess.Collector.Tests;

/// <summary>
/// The read-only law of the customer-side collector, proved two ways.
///
/// 1. A comment-stripped source scan, which can be falsified by feeding it a forbidden call.
/// 2. API-level reflection over the built assembly, which proves the collector publishes no
///    write, method-call or node-management path and never hands a caller a stack session.
///
/// Every forbidden token is assembled from fragments so this file can never satisfy its own
/// scan, and every read strips comments first so a comment can never satisfy a rule.
/// </summary>
[Trait("Gate", "CollectorReadOnlyBoundary")]
public sealed class CollectorReadOnlyBoundaryTests
{
    private const string CollectorProjectDirectory = "Backend/PlantProcess.Collector";
    private const string CollectorProjectFile = "Backend/PlantProcess.Collector/PlantProcess.Collector.csproj";

    private static readonly string[] ForbiddenCallFragments =
    {
        "Wri" + "te",
        "Cal" + "l",
        "HistoryUpd" + "ate",
        "AddNod" + "es",
        "DeleteNod" + "es",
        "AddReferenc" + "es",
        "DeleteReferenc" + "es",
        "SetMonitoringMo" + "de",
        "RegisterNod" + "es"
    };

    private static readonly Regex BlockComment = new(@"/\*[\s\S]*?\*/", RegexOptions.Compiled);
    private static readonly Regex LineComment = new(@"(?<!:)//[^\r\n]*", RegexOptions.Compiled);

    internal static string StripComments(string text)
        => LineComment.Replace(BlockComment.Replace(text, string.Empty), string.Empty);

    /// <summary>Returns every forbidden operation found in executable text. Empty means clean.</summary>
    internal static IReadOnlyList<string> ScanForForbiddenOperations(string sourceText)
    {
        string executable = StripComments(sourceText);
        var findings = new List<string>();

        foreach (string fragment in ForbiddenCallFragments)
        {
            var callPattern = new Regex(@"\.\s*" + Regex.Escape(fragment) + @"(Async)?\s*\(", RegexOptions.None);
            foreach (Match match in callPattern.Matches(executable))
            {
                findings.Add(match.Value.Trim());
            }
        }

        return findings;
    }

    [Fact]
    public void The_collector_source_holds_no_write_call_or_node_management_path()
    {
        var offenders = new List<string>();

        foreach (string file in CollectorSourceFiles())
        {
            IReadOnlyList<string> findings = ScanForForbiddenOperations(File.ReadAllText(file));
            if (findings.Count > 0)
            {
                offenders.Add(Path.GetFileName(file) + ": " + string.Join(", ", findings));
            }
        }

        Assert.True(
            offenders.Count == 0,
            "The customer-side collector must hold no write, method-call or node-management path. Offending files: " +
            string.Join(" | ", offenders));
    }

    [Fact]
    public void The_scan_goes_red_on_a_forbidden_call_and_stays_green_on_a_comment()
    {
        string forbidden = "session." + "Wri" + "teAsync(header, values, default);";
        string commented = "// session." + "Wri" + "teAsync(header, values, default);";

        Assert.NotEmpty(ScanForForbiddenOperations(forbidden));
        Assert.Empty(ScanForForbiddenOperations(commented));
    }

    [Fact]
    public void The_collector_source_holds_no_database_client()
    {
        var offenders = new List<string>();
        string[] databaseTokens = { "Npg" + "sql", "EntityFramework" + "Core", "SqlCli" + "ent" };

        foreach (string file in CollectorSourceFiles())
        {
            string executable = StripComments(File.ReadAllText(file));
            foreach (string token in databaseTokens)
            {
                if (executable.Contains(token, StringComparison.Ordinal))
                {
                    offenders.Add(Path.GetFileName(file) + ": " + token);
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "The customer-side collector must hold no database client. Offending files: " + string.Join(" | ", offenders));
    }

    [Fact]
    public void The_built_collector_assembly_publishes_no_write_call_or_node_management_member()
    {
        var offenders = new List<string>();

        foreach (Type type in CollectorTypes())
        {
            foreach (MemberInfo member in type.GetMembers(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                foreach (string fragment in ForbiddenCallFragments)
                {
                    if (member.Name.Contains(fragment, StringComparison.Ordinal))
                    {
                        offenders.Add(type.FullName + "." + member.Name);
                    }
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "No public collector member may name a write, method-call or node-management operation. Offenders: " +
            string.Join(", ", offenders));
    }

    [Fact]
    public void The_built_collector_assembly_never_hands_a_caller_a_stack_session()
    {
        var offenders = new List<string>();

        foreach (Type type in CollectorTypes())
        {
            foreach (MethodInfo method in type.GetMethods(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                if (IsStackSession(method.ReturnType) ||
                    method.GetParameters().Any(parameter => IsStackSession(parameter.ParameterType)))
                {
                    offenders.Add(type.FullName + "." + method.Name);
                }
            }

            foreach (PropertyInfo property in type.GetProperties(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                if (IsStackSession(property.PropertyType))
                {
                    offenders.Add(type.FullName + "." + property.Name);
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "The collector must never publish the stack session it holds. Offenders: " + string.Join(", ", offenders));
    }

    [Fact]
    public void The_session_runtime_publishes_exactly_the_agreed_operations()
    {
        string[] expected =
        {
            "ConnectAsync",
            "DisconnectAsync",
            "DisposeAsync",
            "get_SourceProfileId",
            "get_ConfigurationVersion",
            "get_State",
            "get_ReconnectCount",
            "get_SessionId",
            "get_LastReceipt"
        };

        string[] actual = typeof(OpcUaCollectorSessionRuntime)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(method => method.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expected.OrderBy(name => name, StringComparer.Ordinal).ToArray(), actual);
    }

    [Fact]
    public void The_collector_host_publishes_no_acquisition_scheduler()
    {
        string[] forbidden = { "Schedul", "Lease", "Fence", "Microbatch", "Subscribe" };

        string[] offenders = typeof(OpcUaCollectorHost)
            .GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Select(member => member.Name)
            .Where(name => forbidden.Any(token => name.Contains(token, StringComparison.Ordinal)))
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            "Continuous acquisition ownership belongs to the acquisition session task, not to this host. Offenders: " +
            string.Join(", ", offenders));
    }

    [Fact]
    public void No_core_project_references_the_collector_or_an_opc_client()
    {
        var offenders = new List<string>();
        string backend = Path.Combine(RepoRoot(), "Backend");

        foreach (string project in Directory.EnumerateFiles(backend, "*.csproj", SearchOption.AllDirectories))
        {
            string relative = Relative(project);
            if (relative.Equals(CollectorProjectFile, StringComparison.OrdinalIgnoreCase) ||
                relative.Contains("PlantProcess.Collector.Tests", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string text = File.ReadAllText(project);
            if (text.Contains("PlantProcess.Collector", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("OPCFoundation", StringComparison.OrdinalIgnoreCase))
            {
                offenders.Add(relative);
            }
        }

        Assert.True(
            offenders.Count == 0,
            "Core must never hold the plant client or the collector. Offending projects: " + string.Join(", ", offenders));
    }

    [Fact]
    public void The_collector_project_references_no_core_project()
    {
        string text = File.ReadAllText(Path.Combine(RepoRoot(), CollectorProjectFile.Replace('/', Path.DirectorySeparatorChar)));

        Assert.DoesNotContain("ProjectReference", text, StringComparison.Ordinal);
    }

    private static IEnumerable<Type> CollectorTypes()
        => typeof(OpcUaCollectorSessionRuntime).Assembly
            .GetTypes()
            .Where(type => type.IsPublic || type.IsNestedPublic);

    private static bool IsStackSession(Type type)
        => type.FullName is not null &&
           (type.FullName.StartsWith("Opc.Ua.Client.ISession", StringComparison.Ordinal) ||
            type.FullName.StartsWith("Opc.Ua.Client.Session", StringComparison.Ordinal) ||
            type.FullName.StartsWith("Opc.Ua.ISessionClient", StringComparison.Ordinal));

    private static IEnumerable<string> CollectorSourceFiles()
    {
        string directory = Path.Combine(RepoRoot(), CollectorProjectDirectory.Replace('/', Path.DirectorySeparatorChar));

        return Directory
            .EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .Where(file => !file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .OrderBy(file => file, StringComparer.Ordinal);
    }

    private static string Relative(string absolutePath)
        => Path.GetRelativePath(RepoRoot(), absolutePath).Replace(Path.DirectorySeparatorChar, '/');

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