using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using PlantProcess.Analytics.Core.Kernel;
using PlantProcess.Application.Temporal;
using PlantProcess.Domain.Entities.Process;
using PlantProcess.Infrastructure.Canonical;
using PlantProcess.Infrastructure.Persistence;
using Xunit;

namespace PlantProcess.Architecture.Tests;

[Trait("Gate", "SourceTimeAuthority")]
public sealed class SourceTimeAuthorityContractTests
{
    private static readonly string[] WindowAndIdentity = { "Id", "EffectiveFromUtc", "EffectiveToUtc" };
    private static readonly string[] RequestExtras = { "EffectiveFromUtc", "EffectiveToUtc", "CreatedBy", "SourceSystem", "SourceRecordId" };

    [Fact]
    public void Response_is_exact_kernel_contract_plus_window()
    {
        var contract = typeof(TimeSignalDeclaration).GetProperties().Select(p => p.Name).Where(n => n != "EqualityContract").OrderBy(n => n).ToArray();
        var response = typeof(SourceTimeAuthorityResponse).GetProperties().Select(p => p.Name).Where(n => n != "EqualityContract" && !WindowAndIdentity.Contains(n)).OrderBy(n => n).ToArray();
        Assert.Equal(contract, response);
    }

    [Fact]
    public void Request_carries_declared_contract_fields_and_no_derived_uncertainty()
    {
        var declared = typeof(TimeSignalDeclaration).GetConstructors().Single(c => c.GetParameters().Length == 9).GetParameters()
            .Select(p => char.ToUpperInvariant(p.Name![0]) + p.Name[1..]).OrderBy(n => n).ToArray();
        var request = typeof(SourceTimeAuthorityDeclarationRequest).GetProperties().Select(p => p.Name)
            .Where(n => n != "EqualityContract" && !RequestExtras.Contains(n)).OrderBy(n => n).ToArray();
        Assert.Equal(declared, request); Assert.DoesNotContain("Uncertainty", request);
    }

    [Fact]
    public void Migration_is_tenant_scoped_function_only_runtime_write_authority()
    {
        var sql = File.ReadAllText(MigrationPath());
        Assert.Contains("ppiq_meta.source_time_authorities", sql);
        Assert.Contains("REFERENCES ppiq_meta.tenants", sql);
        Assert.Contains("SECURITY DEFINER", sql);
        Assert.Contains("SET search_path = pg_catalog, ppiq_meta", sql);
        Assert.Contains("REVOKE INSERT, UPDATE, DELETE", sql);
        Assert.Contains("GRANT SELECT ON ppiq_meta.source_time_authorities", sql);
        Assert.Contains("GRANT EXECUTE ON FUNCTION ppiq_meta.declare_source_time_signal", sql);
        Assert.DoesNotContain("GRANT SELECT, INSERT", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("site_id", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Migration_is_positioned_after_844_and_before_views()
    {
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root(), "Backend", "database", "canonical-migration-order.json")));
        var path = manifest.RootElement.GetProperty("canonicalPath").EnumerateArray().ToArray();
        var sta = path.Single(e => (e.GetProperty("path").GetString() ?? "").EndsWith("_source_time_authority_persistence.sql", StringComparison.Ordinal));
        var cancel = path.Single(e => (e.GetProperty("path").GetString() ?? "").EndsWith("/844_job_cancellation_request_contract.sql", StringComparison.Ordinal));
        var firstView = path.Where(e => (e.GetProperty("path").GetString() ?? "").StartsWith("Backend/database/views/", StringComparison.Ordinal)).Min(e => e.GetProperty("position").GetInt32());
        Assert.True(sta.GetProperty("position").GetInt32() > cancel.GetProperty("position").GetInt32());
        Assert.True(sta.GetProperty("position").GetInt32() < firstView);
        Assert.Contains("ppiq_meta.source_time_authorities", sta.GetProperty("createsTables").EnumerateArray().Select(e => e.GetString()));
    }

    [Fact]
    public void Api_and_store_are_tenant_scoped_and_function_only()
    {
        var root = Root();
        var api = File.ReadAllText(Path.Combine(root, "Backend", "PlantProcess.Api", "Endpoints", "SourceTime", "SourceTimeAuthorityEndpoints.cs"));
        var program = File.ReadAllText(Path.Combine(root, "Backend", "PlantProcess.Api", "Program.cs"));
        var store = File.ReadAllText(Path.Combine(root, "Backend", "PlantProcess.Infrastructure", "Temporal", "SourceTimeAuthorityStore.cs"));
        Assert.Contains("TenantClaims.Resolve(httpContext.User)", api);
        Assert.Contains("/api/source-time/authorities", api);
        Assert.Contains("MapSourceTimeAuthorityEndpoints(app)", program);
        Assert.Contains("TenantSql.AssertScoped", store);
        Assert.Contains("ppiq_meta.declare_source_time_signal", store);
        Assert.DoesNotContain("INSERT INTO", store, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Route_is_permissioned_and_nonanonymous()
    {
        var field = typeof(PlantProcess.Api.Security.AccessControlMiddleware).GetField("Matrix", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(field);
        var matrix = ((string Prefix, string[] Methods, string Permission, bool Anonymous)[])field!.GetValue(null)!;
        foreach (var method in new[] { "GET", "POST" })
        {
            var entry = matrix.OrderByDescending(e => e.Prefix.Length).First(e => "/api/source-time/authorities/SRC/signal".StartsWith(e.Prefix, StringComparison.OrdinalIgnoreCase) && e.Methods.Contains(method, StringComparer.OrdinalIgnoreCase));
            Assert.Equal("/api/source-time", entry.Prefix); Assert.Equal("source.configure", entry.Permission); Assert.False(entry.Anonymous);
        }
    }

    [Fact]
    public void Receipt_is_shadow_and_source_server_are_optional_authored_fields()
    {
        var catalogue = new CanonicalEntityCatalog(ModelOnlyContext());
        var fields = catalogue.ProjectionFieldsOf("ParameterObservation").ToDictionary(f => f.Name, StringComparer.Ordinal);
        Assert.Null(typeof(ParameterObservation).GetProperty("IngestedAtUtc"));
        Assert.True(fields["IngestedAtUtc"].IsSystemOwned); Assert.False(fields["IngestedAtUtc"].IsAuthorWritable);
        foreach (var name in new[] { "SourceTimestampUtc", "ServerTimestampUtc" }) { Assert.True(fields[name].IsAuthorWritable); Assert.False(fields[name].IsRequired); }
        var entity = ModelOnlyContext().Model.FindEntityType(typeof(ParameterObservation))!;
        var ingested = entity.FindProperty("IngestedAtUtc")!; Assert.True(ingested.IsShadowProperty()); Assert.Equal("now()", ingested.GetDefaultValueSql());
    }

    [Fact]
    public void Projector_consumes_raw_time_through_frozen_kernel_and_infrastructure_provider()
    {
        var root = Root();
        var source = File.ReadAllText(Path.Combine(root, "Backend", "PlantProcess.Application", "Integration", "Services", "Mapping", "MappingRowProjector.cs"));
        var contracts = File.ReadAllText(Path.Combine(root, "Backend", "PlantProcess.Application", "Temporal", "SourceTimeAuthorityContracts.cs"));
        var store = File.ReadAllText(Path.Combine(root, "Backend", "PlantProcess.Infrastructure", "Temporal", "SourceTimeAuthorityStore.cs"));
        var infrastructureDi = File.ReadAllText(Path.Combine(root, "Backend", "PlantProcess.Infrastructure", "DependencyInjection.cs"));

        Assert.Contains("SourceTimeRuntimeResolver.Resolve", source);
        Assert.Contains("TimeRole.Effective", source);
        Assert.Contains("ISourceTimeAuthorityRegistryProvider sourceTimeAuthority", source);
        Assert.Contains("_sourceTimeAuthority = sourceTimeAuthority", source);
        Assert.Contains("public MappingRowProjector(IPlantProcessDbContext dbContext)", source);
        Assert.Contains("private readonly ISourceTimeAuthorityRegistryProvider _sourceTimeAuthority = null!;", source);
        Assert.Contains("_sourceTimeAuthority.LoadRegistryAsync", source);
        Assert.Contains("SourceTimeAuthorityStore : ISourceTimeAuthorityRegistryProvider", store);
        Assert.Contains("ISourceTimeAuthorityRegistryProvider, PlantProcess.Infrastructure.Temporal.SourceTimeAuthorityStore", infrastructureDi);

        // Application owns contract/orchestration only. Relational implementation
        // remains in Infrastructure.
        Assert.DoesNotContain("DbContextSourceTimeAuthorityRegistryProvider", contracts);
        Assert.DoesNotContain("GetDbConnection", contracts);
        Assert.DoesNotContain("GetDbTransaction", contracts);
        Assert.DoesNotContain("Npgsql", contracts);

        Assert.DoesNotContain("RequiredDateTime(\"ObservedAtUtc\")", source);
        Assert.DoesNotContain("TimeZoneInfo.Local", source);
        Assert.DoesNotContain("OptionalDateTime(\"SourceTimestampUtc\")", source);
        Assert.DoesNotContain("OptionalDateTime(\"ServerTimestampUtc\")", source);
    }

    private static string MigrationPath() => Directory.GetFiles(Path.Combine(Root(), "Backend", "database", "scripts"), "*_source_time_authority_persistence.sql").Single();
    private static PlantProcessDbContext ModelOnlyContext() => new(new DbContextOptionsBuilder<PlantProcessDbContext>().UseNpgsql("Host=localhost;Database=model_only;Username=model_only;Password=model_only").UseSnakeCaseNamingConvention().Options);
    private static string Root() { var d = new DirectoryInfo(AppContext.BaseDirectory); while (d is not null) { if (Directory.Exists(Path.Combine(d.FullName, "Backend", "database"))) return d.FullName; d=d.Parent; } throw new InvalidOperationException("Repository root not found"); }
}