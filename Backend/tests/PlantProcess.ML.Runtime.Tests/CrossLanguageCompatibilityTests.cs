using System.Text.Json;
using PlantProcess.ML.Runtime;
using Xunit;

namespace PlantProcess.ML.Runtime.Tests;

/// <summary>
/// Cross-language compatibility. These JSON documents were produced by the Python
/// runtime and pasted here verbatim. They are not hand-written approximations, so a
/// silent divergence between the two sides fails this suite rather than production.
/// </summary>
public sealed class CrossLanguageCompatibilityTests
{
    private const string PythonSuccessManifest = @"{
  ""analysis_terminal_state"": null,
  ""artifacts"": [
    {
      ""artifact_id"": ""model-1"",
      ""artifact_kind"": ""model"",
      ""byte_size"": 1024,
      ""content_hash"": ""deadbeef"",
      ""uri"": ""/tmp/model.bin""
    }
  ],
  ""code_identity"": ""commit-abc123"",
  ""completed_at_utc"": ""2026-08-12T12:22:39.139359+00:00"",
  ""duration_seconds"": 5.1e-05,
  ""input_hashes"": {
    ""snap-1"": ""558d283da74bd4279e463ec794d4fbe6b4bab02f0fc4d40705d2a2024e1b3402""
  },
  ""job_id"": ""job-cross-1"",
  ""metrics"": {
    ""auc"": 0.834,
    ""brier"": 0.112
  },
  ""outcome"": ""succeeded"",
  ""protocol"": ""ppiq.mljob/1"",
  ""refusal_code"": ""none"",
  ""refusal_reason"": """",
  ""resumed_from_checkpoint"": null,
  ""runtime_version"": ""ppiq_ml 0.1.0"",
  ""seed"": 20260812,
  ""started_at_utc"": ""2026-08-12T12:22:39.139177+00:00"",
  ""warnings"": [
    ""baseline beat the challenger on calibration""
  ]
}";

    private const string PythonRefusedManifest = @"{
  ""analysis_terminal_state"": null,
  ""artifacts"": [],
  ""code_identity"": ""commit-abc123"",
  ""completed_at_utc"": ""2026-08-12T12:22:39.141231+00:00"",
  ""duration_seconds"": 8.2e-05,
  ""input_hashes"": {
    ""snap-1"": ""558d283da74bd4279e463ec794d4fbe6b4bab02f0fc4d40705d2a2024e1b3402""
  },
  ""job_id"": ""job-cross-1"",
  ""metrics"": {},
  ""outcome"": ""refused"",
  ""protocol"": ""ppiq.mljob/1"",
  ""refusal_code"": ""eligibility_not_met"",
  ""refusal_reason"": ""The declared outcome carries 12 labelled units against a floor of 500."",
  ""resumed_from_checkpoint"": null,
  ""runtime_version"": ""ppiq_ml 0.1.0"",
  ""seed"": 20260812,
  ""started_at_utc"": ""2026-08-12T12:22:39.141148+00:00"",
  ""warnings"": []
}";

    private const string PythonJobSpec = @"{
  ""cancellation_file"": null,
  ""checkpoint_directory"": null,
  ""code_identity"": ""commit-abc123"",
  ""inputs"": [
    {
      ""artifact_format"": ""parquet"",
      ""artifact_id"": ""snap-1"",
      ""byte_size"": 21,
      ""content_hash"": ""558d283da74bd4279e463ec794d4fbe6b4bab02f0fc4d40705d2a2024e1b3402"",
      ""uri"": ""/tmp/tmpohiedlhi/snap.parquet""
    }
  ],
  ""job_id"": ""job-cross-1"",
  ""model_family"": ""mf04_supervised"",
  ""output_directory"": ""/tmp/tmpohiedlhi/out"",
  ""parameters"": {
    ""n_estimators"": 200
  },
  ""protocol"": ""ppiq.mljob/1"",
  ""resources"": {
    ""gpu_required"": false,
    ""max_memory_mb"": 8192,
    ""max_wall_clock_seconds"": 600.0
  },
  ""seed"": 20260812,
  ""semantic_manifest_id"": ""manifest-7"",
  ""site_id"": ""site-1"",
  ""tenant_id"": ""tenant-a""
}";

    [Fact]
    public void This_side_parses_a_manifest_the_python_runtime_actually_wrote()
    {
        var manifest = ResultManifest.FromJson(PythonSuccessManifest);

        Assert.Equal(MlJobProtocol.Id, manifest.Protocol);
        Assert.Equal("job-cross-1", manifest.JobId);
        Assert.Equal(JobOutcome.Succeeded, manifest.OutcomeValue);
        Assert.Equal(MlRefusalCode.None, manifest.RefusalCodeValue);
        Assert.Equal("commit-abc123", manifest.CodeIdentity);
        Assert.Equal(20260812, manifest.Seed);
        manifest.ValidateRefusalConsistency();
    }

    [Fact]
    public void Metrics_artifacts_and_warnings_survive_the_language_boundary()
    {
        var manifest = ResultManifest.FromJson(PythonSuccessManifest);

        Assert.Equal(0.834, manifest.Metrics["auc"], 9);
        Assert.Equal(0.112, manifest.Metrics["brier"], 9);
        Assert.Single(manifest.Artifacts);
        Assert.Equal("model-1", manifest.Artifacts[0].ArtifactId);
        Assert.Equal(1024, manifest.Artifacts[0].ByteSize);
        Assert.Contains("baseline beat the challenger on calibration", manifest.Warnings);
        Assert.Equal("snap-1", manifest.InputHashes.Keys.Single());
    }

    [Fact]
    public void A_python_refusal_arrives_with_its_code_and_its_sentence()
    {
        var manifest = ResultManifest.FromJson(PythonRefusedManifest);

        Assert.Equal(JobOutcome.Refused, manifest.OutcomeValue);
        Assert.Equal(MlRefusalCode.EligibilityNotMet, manifest.RefusalCodeValue);
        Assert.Contains("500", manifest.RefusalReason);
        manifest.ValidateRefusalConsistency();
    }

    [Fact]
    public void A_refusal_is_not_a_failure_and_the_two_never_collapse()
    {
        var refused = ResultManifest.FromJson(PythonRefusedManifest);

        Assert.Equal(JobOutcome.Refused, refused.OutcomeValue);
        Assert.NotEqual(JobOutcome.Failed, refused.OutcomeValue);
        Assert.NotEqual(JobOutcome.Succeeded, refused.OutcomeValue);
    }

    [Fact]
    public void A_job_spec_this_side_writes_carries_every_field_python_requires()
    {
        var pythonSpec = JsonSerializer.Deserialize<JsonElement>(PythonJobSpec);
        var ours = new JobSpec
        {
            JobId = "job-1", TenantId = "t", SiteId = "s", ModelFamily = "mf04_supervised",
            OutputDirectory = "/tmp/out", Seed = 1, CodeIdentity = "c",
            Resources = new ResourceBudget(600.0)
        };
        var oursParsed = JsonSerializer.Deserialize<JsonElement>(ours.ToJson());

        foreach (var required in pythonSpec.EnumerateObject())
        {
            Assert.True(oursParsed.TryGetProperty(required.Name, out _),
                $"The C# job spec omits '{required.Name}', which the Python runtime reads.");
        }
    }

    [Fact]
    public void The_two_sides_agree_on_the_protocol_identity()
    {
        var pythonSpec = JsonSerializer.Deserialize<JsonElement>(PythonJobSpec);
        Assert.Equal(MlJobProtocol.Id, pythonSpec.GetProperty("protocol").GetString());

        var pythonManifest = JsonSerializer.Deserialize<JsonElement>(PythonSuccessManifest);
        Assert.Equal(MlJobProtocol.Id, pythonManifest.GetProperty("protocol").GetString());
    }
}
