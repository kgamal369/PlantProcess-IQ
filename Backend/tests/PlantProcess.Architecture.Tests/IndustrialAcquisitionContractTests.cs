// Industrial acquisition static authority gates.
//
// What these protect: the configuration lives on the canonical definition store
// rather than in a private registry, the legacy connector tag catalogue is no
// longer an identity authority for industrial integration, the capability answer
// is read from connector truth instead of restated, and no route reports a
// running acquisition that nothing started.
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using PlantProcess.Application.Definitions;
using PlantProcess.Application.Integration.Acquisition;
using PlantProcess.Infrastructure.Definitions;
using Xunit;

namespace PlantProcess.Architecture.Tests;

[Trait("BacklogTask", "T-268")]
public sealed class IndustrialAcquisitionContractTests
{
    private const string ApplicationDirectory = "Backend/PlantProcess.Application/Integration/Acquisition";
    private const string InfrastructureDirectory = "Backend/PlantProcess.Infrastructure/Integration/Acquisition";
    private const string EndpointsPath = "Backend/PlantProcess.Api/Endpoints/Integration/IndustrialAcquisitionEndpoints.cs";
    private const string ServicePath = InfrastructureDirectory + "/AcquisitionConfigurationService.cs";
    private const string StorePath = InfrastructureDirectory + "/IndustrialAcquisitionStore.cs";
    private const string CapabilityPath = ApplicationDirectory + "/AcquisitionCapabilityTruth.cs";

    /// <summary>The legacy connector/tag family that is compatibility only from T-268 onward.</summary>
    private static readonly string[] RetiredIdentityAuthorities =
    {
        "ppiq_connector_tag_catalog", "ppiq_plant_connectors", "historian_tag_mappings"
    };

    // ------------------------------------------------------------- migration

    [Fact]
    [Trait("Gate", "ACQUISITION_MIGRATION")]
    public void The_migration_supersedes_the_kind_check_and_writes_through_governed_functions()
    {
        var sql = File.ReadAllText(MigrationPath());

        Assert.Contains("DROP CONSTRAINT IF EXISTS ck_definition_store_kind", sql, StringComparison.Ordinal);
        Assert.Contains("'acquisition_configuration'", sql, StringComparison.Ordinal);
        Assert.Contains("ppiq_meta.acquisition_configuration_details", sql, StringComparison.Ordinal);
        Assert.Contains("definition_detail_parent_guard('S1', 'acquisition_configuration')", sql, StringComparison.Ordinal);
        Assert.Contains("SECURITY DEFINER", sql, StringComparison.Ordinal);
        Assert.Contains("SET search_path = pg_catalog, ppiq_meta", sql, StringComparison.Ordinal);
        Assert.Contains("REVOKE INSERT, UPDATE, DELETE, TRUNCATE ON ppiq_meta.source_field_revisions FROM plantprocess_app;", sql, StringComparison.Ordinal);
        Assert.Contains("GRANT EXECUTE ON FUNCTION ppiq_meta.declare_source_field_revision", sql, StringComparison.Ordinal);
        Assert.Contains("ux_source_dataset_governance_dataset UNIQUE (source_dataset_definition_id)", sql, StringComparison.Ordinal);

        // The legacy integration tables keep their shape: tenancy convergence for them
        // is not this task's, and a column added here would be a second tenancy record.
        foreach (var legacy in new[] { "connection_profiles", "source_dataset_definitions", "source_field_definitions" })
        {
            Assert.DoesNotContain("ALTER TABLE ppiq_meta." + legacy, sql, StringComparison.Ordinal);
        }
    }

    [Fact]
    [Trait("Gate", "ACQUISITION_MIGRATION_POSITION")]
    public void The_migration_is_on_the_canonical_path_after_source_time_and_before_the_views()
    {
        using var manifest = JsonDocument.Parse(Read("Backend/database/canonical-migration-order.json"));
        var path = manifest.RootElement.GetProperty("canonicalPath").EnumerateArray().ToArray();

        var acquisition = path.Single(e =>
            (e.GetProperty("path").GetString() ?? string.Empty).EndsWith("_industrial_acquisition_configuration.sql", StringComparison.Ordinal));
        var sourceTime = path.Single(e =>
            (e.GetProperty("path").GetString() ?? string.Empty).EndsWith("_source_time_authority_persistence.sql", StringComparison.Ordinal));
        var firstView = path
            .Where(e => (e.GetProperty("path").GetString() ?? string.Empty).StartsWith("Backend/database/views/", StringComparison.Ordinal))
            .Min(e => e.GetProperty("position").GetInt32());

        Assert.True(acquisition.GetProperty("position").GetInt32() > sourceTime.GetProperty("position").GetInt32());
        Assert.True(acquisition.GetProperty("position").GetInt32() < firstView);

        var creates = acquisition.GetProperty("createsTables").EnumerateArray().Select(e => e.GetString()).ToArray();
        foreach (var table in new[]
        {
            "ppiq_meta.acquisition_configuration_details",
            "ppiq_meta.source_dataset_governance",
            "ppiq_meta.source_field_identities",
            "ppiq_meta.source_field_revisions",
            "ppiq_meta.source_layout_revisions",
        })
        {
            Assert.Contains(table, creates);
        }
    }

    // ------------------------------------------------------------- authority

    [Fact]
    [Trait("Gate", "ACQUISITION_SINGLE_DEFINITION_AUTHORITY")]
    public void The_configuration_is_written_through_the_canonical_definition_writer()
    {
        var service = StripComments(Read(ServicePath));

        Assert.Contains("_writer.WriteVersionAsync", service, StringComparison.Ordinal);
        Assert.Contains("DefinitionKind.AcquisitionConfiguration", service, StringComparison.Ordinal);
        Assert.Contains("_writer.PublishAsync", service, StringComparison.Ordinal);

        // No second store, no second version numbering, no second hash.
        Assert.DoesNotContain("INSERT INTO ppiq_meta.definition_versions", service, StringComparison.Ordinal);
        Assert.DoesNotContain("INSERT INTO ppiq_meta.definition_store", service, StringComparison.Ordinal);
        Assert.DoesNotContain("version_number + 1", service, StringComparison.Ordinal);

        Assert.Equal("acquisition_configuration", DefinitionKindRegistry.StorageKindOf(DefinitionKind.AcquisitionConfiguration));
        Assert.Equal("S1", DefinitionKindRegistry.SurfaceOf(DefinitionKind.AcquisitionConfiguration));
    }

    [Fact]
    [Trait("Gate", "ACQUISITION_GOVERNED_WRITES")]
    public void The_store_writes_only_through_the_governed_functions_and_reads_tenant_scoped()
    {
        var store = StripComments(Read(StorePath));

        Assert.Contains("ppiq_meta.govern_source_dataset", store, StringComparison.Ordinal);
        Assert.Contains("ppiq_meta.declare_source_field_revision", store, StringComparison.Ordinal);
        Assert.Contains("ppiq_meta.declare_source_layout_revision", store, StringComparison.Ordinal);
        Assert.Contains("TenantSql.AssertScoped", store, StringComparison.Ordinal);
        Assert.Contains("RequireTransaction", store, StringComparison.Ordinal);

        Assert.DoesNotContain("INSERT INTO", store, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE ppiq_meta", store, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE FROM", store, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Gate", "ACQUISITION_CAPABILITY_TRUTH")]
    public void Capability_answers_are_read_from_connector_truth_and_never_restated()
    {
        var capability = StripComments(Read(CapabilityPath));

        Assert.Contains("HistorianCapabilityRegistry.Get", capability, StringComparison.Ordinal);
        Assert.Contains("ConnectorProviderCatalog.GetProviderTypes", capability, StringComparison.Ordinal);
        Assert.Contains("provider.IsAvailableNow", capability, StringComparison.Ordinal);

        // A capability declared here rather than read would be a second registry.
        Assert.DoesNotContain("new HistorianCapability(", capability, StringComparison.Ordinal);
        Assert.DoesNotContain("Executable: true", capability, StringComparison.Ordinal);

        // The adapter answers the conjunction of the two authorities it reads, whatever
        // they say today: the historian registry for the operation, and provider
        // availability for this environment. No snapshot of either is captured here.
        var subscription = AcquisitionCapabilityTruth.Evaluate(
            AcquisitionCapabilityTruth.HistorianProviderType, AcquisitionCapabilityTruth.Subscription);
        var declared = PlantProcess.Application.Integration.Connectors.HistorianCapabilityRegistry.IsExecutable(
            PlantProcess.Application.Integration.Connectors.HistorianCapabilityRegistry.Subscription);
        var available = PlantProcess.Application.Integration.Connectors.ProviderAvailability.IsAvailableNow(
            AcquisitionCapabilityTruth.HistorianProviderType);
        Assert.Equal(declared, subscription.DeclaredExecutable);
        Assert.Equal(available, subscription.EnvironmentAvailable);
        Assert.Equal(declared && available, subscription.Executable);

        var service = StripComments(Read(ServicePath));
        Assert.Contains("FirstOrDefault(o => !o.DeclaredExecutable)", service, StringComparison.Ordinal);
        Assert.DoesNotContain("FirstOrDefault(o => !o.Executable)", service, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Gate", "ACQUISITION_NO_RUNTIME_CLAIM")]
    public void Activation_never_reports_a_session_this_build_cannot_start()
    {
        var service = StripComments(Read(ServicePath));

        Assert.Contains("RuntimeNotCommissioned", service, StringComparison.Ordinal);
        Assert.DoesNotContain("new AcquisitionActivationAdmission(\n            true,", service.Replace("\r\n", "\n"), StringComparison.Ordinal);

        // No session, receipt, fence or generation is persisted by this task.
        foreach (var runtimeWord in new[] { "acquisition_sessions", "activation_receipt", "owner_generation", "fence_token" })
        {
            Assert.DoesNotContain(runtimeWord, service, StringComparison.OrdinalIgnoreCase);
        }
    }

    // ------------------------------------------------- legacy authority guard

    /// <summary>
    /// From T-268 onward the stable field authority is the canonical field/tag
    /// identity for Industrial Integration. The legacy connector tag catalogue is
    /// compatibility only: nothing on this path may read or write it. The table is
    /// not dropped here - proving zero consumers is a separate, owned piece of work.
    /// </summary>
    [Fact]
    [Trait("Gate", "ACQUISITION_LEGACY_TAG_AUTHORITY_RETIRED")]
    public void No_industrial_integration_path_depends_on_the_legacy_tag_catalogue()
    {
        var scanned = 0;
        var lines = 0;
        var offenders = new List<string>();

        foreach (var file in ImplementationFiles())
        {
            var text = File.ReadAllText(file);
            scanned++;
            lines += text.Count(c => c == '\n');

            var code = StripComments(text);
            foreach (var legacy in RetiredIdentityAuthorities)
            {
                if (code.Contains(legacy, StringComparison.OrdinalIgnoreCase))
                {
                    offenders.Add(Path.GetFileName(file) + " -> " + legacy);
                }
            }
        }

        Assert.True(scanned >= 8, "Only " + scanned + " implementation files were opened; the scan is not covering the slice.");
        Assert.True(lines > 1000, "Only " + lines + " lines were read; the scan is vacuous.");
        Assert.Empty(offenders);

        // NEGATIVE CONTROL. The same comparison, on text that does carry the legacy
        // authority, must find it - otherwise the zero above proves nothing.
        var probe = StripComments("var table = \"ppiq_meta.ppiq_connector_tag_catalog\";");
        Assert.Contains(RetiredIdentityAuthorities[0], probe, StringComparison.OrdinalIgnoreCase);

        // And a comment naming it is not a dependency: the scan reads code, not prose.
        Assert.DoesNotContain(
            RetiredIdentityAuthorities[0],
            StripComments("// legacy ppiq_connector_tag_catalog is compatibility only\nvar x = 1;"),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Gate", "ACQUISITION_NO_PRIVATE_AUTHORITIES")]
    public void The_slice_declares_no_private_scheduler_source_time_or_capability_authority()
    {
        foreach (var file in ImplementationFiles())
        {
            var code = StripComments(File.ReadAllText(file));
            var name = Path.GetFileName(file);

            Assert.DoesNotContain("ImportScheduleExpression", code, StringComparison.Ordinal);
            Assert.DoesNotContain("NextRunAtUtc", code, StringComparison.Ordinal);
            Assert.DoesNotContain("TimeSignalDeclaration", code, StringComparison.Ordinal);
            Assert.DoesNotContain("SourceTimeAuthorityRegistry", code, StringComparison.Ordinal);
            Assert.False(Regex.IsMatch(name, @"T-\d{3}"), "A repository filename must not carry a task ID: " + name);
        }
    }

    // ------------------------------------------------------------- endpoints

    [Fact]
    [Trait("Gate", "ACQUISITION_ROUTES")]
    public void The_frozen_route_family_is_mapped_tenant_scoped_and_permissioned()
    {
        var api = Read(EndpointsPath);
        var program = Read("Backend/PlantProcess.Api/Program.cs");

        Assert.Contains("MapIndustrialAcquisitionEndpoints(app)", program, StringComparison.Ordinal);
        Assert.Contains("TenantClaims.Resolve(httpContext.User)", api, StringComparison.Ordinal);

        foreach (var route in new[]
        {
            "\"/fields\"", "\"/layouts\"", "\"/layouts/{revision:int}\"", "\"/governance\"",
            "\"/acquisition-configurations\"", "\"/acquisition-configurations/{version:int}\"",
            "\"/acquisition-configurations/{version:int}/validate\"",
            "\"/acquisition-configurations/{version:int}/activate\"",
        })
        {
            Assert.Contains(route, api, StringComparison.Ordinal);
        }

        var field = typeof(PlantProcess.Api.Security.AccessControlMiddleware)
            .GetField("Matrix", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(field);

        var matrix = ((string Prefix, string[] Methods, string Permission, bool Anonymous)[])field!.GetValue(null)!;
        foreach (var method in new[] { "GET", "PUT", "POST" })
        {
            var entry = matrix
                .OrderByDescending(e => e.Prefix.Length)
                .First(e => "/api/datasets/00000000-0000-0000-0000-000000000000/fields"
                    .StartsWith(e.Prefix, StringComparison.OrdinalIgnoreCase) &&
                    e.Methods.Contains(method, StringComparer.OrdinalIgnoreCase));

            Assert.Equal("/api/datasets", entry.Prefix);
            Assert.Equal("source.configure", entry.Permission);
            Assert.False(entry.Anonymous);
        }
    }

    // ------------------------------------------------------------- internals

    private static IEnumerable<string> ImplementationFiles()
    {
        var root = Root();
        foreach (var directory in new[] { ApplicationDirectory, InfrastructureDirectory })
        {
            var full = Path.Combine(root, directory.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(Directory.Exists(full), "Expected " + directory + " to exist.");
            foreach (var file in Directory.EnumerateFiles(full, "*.cs", SearchOption.AllDirectories))
            {
                yield return file;
            }
        }

        yield return Path.Combine(root, EndpointsPath.Replace('/', Path.DirectorySeparatorChar));
    }

    private static string MigrationPath() =>
        Directory.GetFiles(
            Path.Combine(Root(), "Backend", "database", "scripts"),
            "*_industrial_acquisition_configuration.sql").Single();

    private static string Read(string relativePath)
    {
        var full = Path.Combine(Root(), relativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(full), "Expected " + relativePath + " to exist. Without it this gate proves nothing.");
        var text = File.ReadAllText(full);
        Assert.False(string.IsNullOrWhiteSpace(text), relativePath + " is empty; the scan would be vacuous.");
        return text;
    }

    private static string StripComments(string source)
    {
        var withoutBlocks = Regex.Replace(source, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
        return string.Join('\n', withoutBlocks
            .Split('\n')
            .Where(l => !l.TrimStart().StartsWith("//", StringComparison.Ordinal) &&
                        !l.TrimStart().StartsWith("///", StringComparison.Ordinal)));
    }

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, ".git")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory!.FullName;
    }
}
