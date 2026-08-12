using System.Text.Json;
using System.Text.Json.Serialization;

namespace PlantProcess.ML.Runtime;

public sealed record ProducedArtifact(
    [property: JsonPropertyName("artifact_id")] string ArtifactId,
    [property: JsonPropertyName("uri")] string Uri,
    [property: JsonPropertyName("content_hash")] string ContentHash,
    [property: JsonPropertyName("artifact_kind")] string ArtifactKind,
    [property: JsonPropertyName("byte_size")] long ByteSize = 0);

/// <summary>
/// The structured result the Python runtime writes and this side reads.
/// <para>
/// This file is the authority on what happened. stdout and stderr are diagnostics.
/// A process that prints SUCCESS and writes no valid manifest has failed.
/// </para>
/// </summary>
public sealed record ResultManifest
{
    [JsonPropertyName("protocol")] public string Protocol { get; init; } = string.Empty;
    [JsonPropertyName("job_id")] public string JobId { get; init; } = string.Empty;
    [JsonPropertyName("outcome")] public string Outcome { get; init; } = string.Empty;
    [JsonPropertyName("started_at_utc")] public string StartedAtUtc { get; init; } = string.Empty;
    [JsonPropertyName("completed_at_utc")] public string CompletedAtUtc { get; init; } = string.Empty;
    [JsonPropertyName("duration_seconds")] public double DurationSeconds { get; init; }
    [JsonPropertyName("code_identity")] public string CodeIdentity { get; init; } = string.Empty;
    [JsonPropertyName("seed")] public int Seed { get; init; }
    [JsonPropertyName("runtime_version")] public string RuntimeVersion { get; init; } = string.Empty;
    [JsonPropertyName("refusal_code")] public string RefusalCode { get; init; } = "none";
    [JsonPropertyName("refusal_reason")] public string RefusalReason { get; init; } = string.Empty;
    [JsonPropertyName("artifacts")] public IReadOnlyList<ProducedArtifact> Artifacts { get; init; }
        = Array.Empty<ProducedArtifact>();
    [JsonPropertyName("metrics")] public IReadOnlyDictionary<string, double> Metrics { get; init; }
        = new Dictionary<string, double>();

    /// <summary>
    /// The analysis-side terminal state, when the job ran an analysis. Distinct from
    /// Outcome: a succeeded job may carry an honest insufficient-data result.
    /// </summary>
    [JsonPropertyName("analysis_terminal_state")] public string? AnalysisTerminalState { get; init; }

    [JsonPropertyName("input_hashes")] public IReadOnlyDictionary<string, string> InputHashes { get; init; }
        = new Dictionary<string, string>();
    [JsonPropertyName("warnings")] public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
    [JsonPropertyName("resumed_from_checkpoint")] public string? ResumedFromCheckpoint { get; init; }

    [JsonIgnore] public JobOutcome OutcomeValue => WireNames.OutcomeFromWire(Outcome);
    [JsonIgnore] public MlRefusalCode RefusalCodeValue => WireNames.RefusalFromWire(RefusalCode);

    public static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public string ToJson() => JsonSerializer.Serialize(this, Json);

    /// <summary>
    /// Parse and validate a manifest. Every failure path throws rather than returning a
    /// partially trusted object, because a half-understood manifest is worse than none.
    /// </summary>
    public static ResultManifest FromJson(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new MlProtocolException(MlRefusalCode.MalformedJobSpec,
                "The result manifest is empty. The process produced no authority for its outcome.");

        ResultManifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<ResultManifest>(text, Json);
        }
        catch (JsonException exception)
        {
            throw new MlProtocolException(MlRefusalCode.MalformedJobSpec,
                $"The result manifest is not valid JSON: {exception.Message}");
        }

        if (manifest is null)
            throw new MlProtocolException(MlRefusalCode.MalformedJobSpec, "The result manifest is empty.");

        if (manifest.Protocol != MlJobProtocol.Id)
            throw new MlProtocolException(MlRefusalCode.ProtocolVersionMismatch,
                $"The result manifest declares protocol '{manifest.Protocol}'; this runtime "
                + $"speaks '{MlJobProtocol.Id}'.");

        if (string.IsNullOrWhiteSpace(manifest.JobId))
            throw new MlProtocolException(MlRefusalCode.MalformedJobSpec,
                "The result manifest carries no job id.");

        // Throws on an unknown value rather than defaulting to something plausible.
        _ = manifest.OutcomeValue;
        _ = manifest.RefusalCodeValue;

        return manifest;
    }

    /// <summary>A refusal must carry a code and a sentence. A success must carry neither.</summary>
    public void ValidateRefusalConsistency()
    {
        if (OutcomeValue == JobOutcome.Refused)
        {
            if (RefusalCodeValue == MlRefusalCode.None)
                throw new MlProtocolException(MlRefusalCode.MalformedJobSpec,
                    "A refused job must carry a refusal code.");
            if (string.IsNullOrWhiteSpace(RefusalReason))
                throw new MlProtocolException(MlRefusalCode.MalformedJobSpec,
                    "A refused job must carry a written reason.");
        }

        if (OutcomeValue == JobOutcome.Succeeded && RefusalCodeValue != MlRefusalCode.None)
            throw new MlProtocolException(MlRefusalCode.MalformedJobSpec,
                "A succeeded job must not carry a refusal code.");
    }
}
