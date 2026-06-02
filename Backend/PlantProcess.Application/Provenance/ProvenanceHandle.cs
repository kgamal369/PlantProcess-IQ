namespace PlantProcess.Application.Provenance;

/// <summary>The kind of artifact a surfaced claim resolves to (Doctrine §2).</summary>
public enum ProvenanceKind
{
    Finding,
    JobRun,
    Dataset,
    SourceTable,
    DocumentSection
}

/// <summary>A typed, resolvable pointer from a claim to the artifact that backs it.</summary>
public sealed record ProvenanceHandle(ProvenanceKind Kind, string Id, string? Detail = null)
{
    public string Token => $"{Kind}:{Id}";

    public static ProvenanceHandle Finding(string id, string? detail = null) => new(ProvenanceKind.Finding, id, detail);
    public static ProvenanceHandle JobRun(string id, string? detail = null) => new(ProvenanceKind.JobRun, id, detail);
    public static ProvenanceHandle Dataset(string id, string? detail = null) => new(ProvenanceKind.Dataset, id, detail);
    public static ProvenanceHandle SourceTable(string id, string? detail = null) => new(ProvenanceKind.SourceTable, id, detail);
    public static ProvenanceHandle DocumentSection(string id, string? detail = null) => new(ProvenanceKind.DocumentSection, id, detail);
}

/// <summary>The outcome of resolving a handle against the live store.</summary>
public sealed record ProvenanceResolution(ProvenanceHandle Handle, bool Exists, bool IsLive, string? ArtifactRef, string? Reason)
{
    public bool Resolvable => Exists;

    public static ProvenanceResolution Missing(ProvenanceHandle h, string reason) => new(h, false, false, null, reason);
    public static ProvenanceResolution Live(ProvenanceHandle h, string artifactRef) => new(h, true, true, artifactRef, null);
    public static ProvenanceResolution Seed(ProvenanceHandle h, string artifactRef) => new(h, true, false, artifactRef, "Artifact origin is seed/synthetic.");
}

public enum ClaimValueKind { Numeric, Qualitative }

/// <summary>A single numeric or qualitative value the product intends to surface, with its evidence handle.</summary>
public sealed record Claim(string Key, ClaimValueKind ValueKind, object? Value, ProvenanceHandle? Handle, bool IsLiveSurface = true);

public sealed record RejectedClaim(Claim Claim, string Reason);

/// <summary>The partitioned result of running claims through the guard.</summary>
public sealed record ClaimGuardOutcome(IReadOnlyList<Claim> Accepted, IReadOnlyList<RejectedClaim> Rejected)
{
    public bool AllAccepted => Rejected.Count == 0;
}