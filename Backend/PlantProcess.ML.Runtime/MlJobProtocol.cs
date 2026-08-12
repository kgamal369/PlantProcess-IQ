namespace PlantProcess.ML.Runtime;

/// <summary>
/// Wire identity of the C# to Python ML job protocol. The Python runtime pins the same
/// string. A mismatch is refused before any payload is interpreted, so an old runtime
/// can never be fed a new specification.
/// </summary>
public static class MlJobProtocol
{
    public const string Name = "ppiq.mljob";
    public const int Version = 1;
    public const string Id = Name + "/" + "1";

    public const string ManifestFileName = "result_manifest.json";
}

/// <summary>
/// How the EXECUTION ended. This is not what the analysis concluded.
/// A job can succeed while the analysis it ran honestly refuses to produce a finding.
/// </summary>
public enum JobOutcome
{
    Succeeded,

    /// <summary>The runtime declined to compute, for a stated and governed reason. Valid, not an error.</summary>
    Refused,

    /// <summary>Something went wrong that the runtime did not anticipate.</summary>
    Failed,

    Cancelled,

    /// <summary>Set by this side when the wall clock exceeded the budget. A timed-out process cannot report about itself.</summary>
    TimedOut
}

/// <summary>
/// Why the runtime declined. EXECUTION-side reasons only. Statistical-method reasons and
/// capability shortfalls live in their own code sets and never appear here.
/// </summary>
public enum MlRefusalCode
{
    None,
    ProtocolVersionMismatch,
    MalformedJobSpec,
    ArtifactMissing,
    ArtifactHashMismatch,
    UnsupportedModelFamily,
    EligibilityNotMet,
    OutputLocationUnwritable
}

/// <summary>Raised when a payload cannot be interpreted under this protocol version.</summary>
public sealed class MlProtocolException : Exception
{
    public MlProtocolException(MlRefusalCode code, string message) : base(message) => Code = code;

    public MlRefusalCode Code { get; }
}

/// <summary>
/// Converts between the C# enum names and the snake_case wire values Python writes.
/// The wire form is the contract; the enum spelling is a local convenience.
/// </summary>
public static class WireNames
{
    public static string ToWire(JobOutcome outcome) => outcome switch
    {
        JobOutcome.Succeeded => "succeeded",
        JobOutcome.Refused => "refused",
        JobOutcome.Failed => "failed",
        JobOutcome.Cancelled => "cancelled",
        JobOutcome.TimedOut => "timed_out",
        _ => throw new ArgumentOutOfRangeException(nameof(outcome))
    };

    public static JobOutcome OutcomeFromWire(string wire) => wire switch
    {
        "succeeded" => JobOutcome.Succeeded,
        "refused" => JobOutcome.Refused,
        "failed" => JobOutcome.Failed,
        "cancelled" => JobOutcome.Cancelled,
        "timed_out" => JobOutcome.TimedOut,
        _ => throw new MlProtocolException(MlRefusalCode.MalformedJobSpec,
            $"Unknown job outcome '{wire}'. The manifest was not accepted.")
    };

    public static string ToWire(MlRefusalCode code) => code switch
    {
        MlRefusalCode.None => "none",
        MlRefusalCode.ProtocolVersionMismatch => "protocol_version_mismatch",
        MlRefusalCode.MalformedJobSpec => "malformed_job_spec",
        MlRefusalCode.ArtifactMissing => "artifact_missing",
        MlRefusalCode.ArtifactHashMismatch => "artifact_hash_mismatch",
        MlRefusalCode.UnsupportedModelFamily => "unsupported_model_family",
        MlRefusalCode.EligibilityNotMet => "eligibility_not_met",
        MlRefusalCode.OutputLocationUnwritable => "output_location_unwritable",
        _ => throw new ArgumentOutOfRangeException(nameof(code))
    };

    public static MlRefusalCode RefusalFromWire(string wire) => wire switch
    {
        "none" => MlRefusalCode.None,
        "protocol_version_mismatch" => MlRefusalCode.ProtocolVersionMismatch,
        "malformed_job_spec" => MlRefusalCode.MalformedJobSpec,
        "artifact_missing" => MlRefusalCode.ArtifactMissing,
        "artifact_hash_mismatch" => MlRefusalCode.ArtifactHashMismatch,
        "unsupported_model_family" => MlRefusalCode.UnsupportedModelFamily,
        "eligibility_not_met" => MlRefusalCode.EligibilityNotMet,
        "output_location_unwritable" => MlRefusalCode.OutputLocationUnwritable,
        _ => throw new MlProtocolException(MlRefusalCode.MalformedJobSpec,
            $"Unknown refusal code '{wire}'. The manifest was not accepted.")
    };
}
