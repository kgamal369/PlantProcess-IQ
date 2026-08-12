using PlantProcess.ML.Runtime;
using Xunit;

namespace PlantProcess.ML.Runtime.Tests;

/// <summary>
/// The C# side of the versioned job protocol. Refusal before interpretation, named
/// missing fields, and the rule that a refusal and a success cannot look alike.
/// </summary>
public sealed class ProtocolContractTests
{
    private static JobSpec Valid() => new()
    {
        JobId = "job-1", TenantId = "tenant-a", SiteId = "site-1",
        ModelFamily = "mf04_supervised", OutputDirectory = "/tmp/out",
        Seed = 20260812, CodeIdentity = "commit-abc",
        Resources = new ResourceBudget(600.0, 8192, false)
    };

    [Fact]
    public void The_protocol_identity_is_name_and_version()
    {
        Assert.Equal("ppiq.mljob/1", MlJobProtocol.Id);
        Assert.Equal(1, MlJobProtocol.Version);
    }

    [Fact]
    public void A_job_spec_round_trips_without_loss()
    {
        var original = Valid() with { SemanticManifestId = "manifest-7" };
        var restored = JobSpec.FromJson(original.ToJson());

        Assert.Equal(original.JobId, restored.JobId);
        Assert.Equal(original.TenantId, restored.TenantId);
        Assert.Equal(original.Seed, restored.Seed);
        Assert.Equal(original.SemanticManifestId, restored.SemanticManifestId);
        Assert.Equal(original.Resources.MaxWallClockSeconds, restored.Resources.MaxWallClockSeconds);
    }

    [Fact]
    public void A_future_protocol_is_refused_before_the_payload_is_interpreted()
    {
        var json = Valid().ToJson().Replace("\"ppiq.mljob/1\"", "\"ppiq.mljob/99\"");

        var error = Assert.Throws<MlProtocolException>(() => JobSpec.FromJson(json));

        Assert.Equal(MlRefusalCode.ProtocolVersionMismatch, error.Code);
        Assert.Contains("was not interpreted", error.Message);
    }

    [Fact]
    public void Malformed_json_is_refused_not_crashed()
    {
        var error = Assert.Throws<MlProtocolException>(() => JobSpec.FromJson("{ this is not json"));
        Assert.Equal(MlRefusalCode.MalformedJobSpec, error.Code);
    }

    [Fact]
    public void A_spec_missing_required_fields_names_them()
    {
        var json = Valid().ToJson()
            .Replace("\"code_identity\": \"commit-abc\"", "\"code_identity\": \"\"")
            .Replace("\"site_id\": \"site-1\"", "\"site_id\": \"\"");

        var error = Assert.Throws<MlProtocolException>(() => JobSpec.FromJson(json));

        Assert.Equal(MlRefusalCode.MalformedJobSpec, error.Code);
        Assert.Contains("code_identity", error.Message);
        Assert.Contains("site_id", error.Message);
    }

    [Fact]
    public void A_spec_without_a_wall_clock_budget_is_refused()
    {
        var json = Valid().ToJson().Replace("\"max_wall_clock_seconds\": 600", "\"max_wall_clock_seconds\": 0");
        var error = Assert.Throws<MlProtocolException>(() => JobSpec.FromJson(json));
        Assert.Equal(MlRefusalCode.MalformedJobSpec, error.Code);
    }

    [Fact]
    public void An_empty_manifest_is_refused_with_a_sentence_about_authority()
    {
        var error = Assert.Throws<MlProtocolException>(() => ResultManifest.FromJson("   "));
        Assert.Contains("no authority for its outcome", error.Message);
    }

    [Fact]
    public void An_unknown_outcome_is_refused_rather_than_defaulted()
    {
        var json = "{\"protocol\":\"ppiq.mljob/1\",\"job_id\":\"j\",\"outcome\":\"probably_fine\","
                   + "\"duration_seconds\":1.0,\"refusal_code\":\"none\"}";

        var error = Assert.Throws<MlProtocolException>(() => ResultManifest.FromJson(json));
        Assert.Contains("probably_fine", error.Message);
    }

    [Fact]
    public void A_manifest_from_a_foreign_protocol_is_refused()
    {
        var json = "{\"protocol\":\"ppiq.mljob/99\",\"job_id\":\"j\",\"outcome\":\"succeeded\","
                   + "\"duration_seconds\":1.0,\"refusal_code\":\"none\"}";

        var error = Assert.Throws<MlProtocolException>(() => ResultManifest.FromJson(json));
        Assert.Equal(MlRefusalCode.ProtocolVersionMismatch, error.Code);
    }

    [Fact]
    public void A_refusal_without_a_code_is_rejected()
    {
        var manifest = new ResultManifest
        {
            Protocol = MlJobProtocol.Id, JobId = "j", Outcome = "refused", RefusalCode = "none"
        };
        Assert.Throws<MlProtocolException>(() => manifest.ValidateRefusalConsistency());
    }

    [Fact]
    public void A_refusal_without_a_sentence_is_rejected()
    {
        var manifest = new ResultManifest
        {
            Protocol = MlJobProtocol.Id, JobId = "j", Outcome = "refused",
            RefusalCode = "eligibility_not_met", RefusalReason = "   "
        };
        Assert.Throws<MlProtocolException>(() => manifest.ValidateRefusalConsistency());
    }

    [Fact]
    public void A_success_carrying_a_refusal_code_is_rejected()
    {
        var manifest = new ResultManifest
        {
            Protocol = MlJobProtocol.Id, JobId = "j", Outcome = "succeeded",
            RefusalCode = "eligibility_not_met"
        };
        Assert.Throws<MlProtocolException>(() => manifest.ValidateRefusalConsistency());
    }

    [Fact]
    public void Job_outcome_and_analysis_state_are_different_axes()
    {
        var manifest = new ResultManifest
        {
            Protocol = MlJobProtocol.Id, JobId = "j", Outcome = "succeeded",
            AnalysisTerminalState = "InsufficientData"
        };

        manifest.ValidateRefusalConsistency();
        Assert.Equal(JobOutcome.Succeeded, manifest.OutcomeValue);
        Assert.Equal("InsufficientData", manifest.AnalysisTerminalState);
    }

    [Fact]
    public void Every_outcome_and_refusal_code_round_trips_through_the_wire_form()
    {
        foreach (var outcome in Enum.GetValues<JobOutcome>())
            Assert.Equal(outcome, WireNames.OutcomeFromWire(WireNames.ToWire(outcome)));

        foreach (var code in Enum.GetValues<MlRefusalCode>())
            Assert.Equal(code, WireNames.RefusalFromWire(WireNames.ToWire(code)));
    }

    [Fact]
    public void The_runtime_project_holds_no_database_or_presentation_dependency()
    {
        var referenced = typeof(JobSpec).Assembly.GetReferencedAssemblies()
            .Select(r => r.Name ?? string.Empty).ToArray();

        foreach (var forbidden in new[]
                 { "Npgsql", "Microsoft.EntityFrameworkCore", "PlantProcess.Infrastructure",
                   "PlantProcess.Api", "PlantProcess.Application" })
            Assert.DoesNotContain(referenced,
                r => r.StartsWith(forbidden, StringComparison.OrdinalIgnoreCase));
    }
}
