namespace PlantProcess.Application.Provenance;

/// <summary>
/// Doctrine §2 render guard. Inspects outgoing numeric/qualitative claims and refuses to emit any
/// claim that lacks a resolvable handle, or whose handle resolves to a non-live (seed/synthetic)
/// artifact on a live surface. Callers serialize ONLY the accepted claims.
/// </summary>
public sealed class ProvenanceClaimGuard
{
    private readonly IProvenanceResolver _resolver;

    public ProvenanceClaimGuard(IProvenanceResolver resolver) => _resolver = resolver;

    public async Task<ClaimGuardOutcome> InspectAsync(IReadOnlyList<Claim> claims, CancellationToken cancellationToken)
    {
        var accepted = new List<Claim>(claims.Count);
        var rejected = new List<RejectedClaim>();

        foreach (var claim in claims)
        {
            if (claim.Handle is null)
            {
                rejected.Add(new RejectedClaim(claim, "Claim carries no provenance handle and cannot be rendered."));
                continue;
            }

            var resolution = await _resolver.ResolveAsync(claim.Handle, cancellationToken);

            if (!resolution.Resolvable)
            {
                rejected.Add(new RejectedClaim(claim, resolution.Reason ?? "Provenance handle does not resolve."));
                continue;
            }

            if (claim.IsLiveSurface && !resolution.IsLive)
            {
                rejected.Add(new RejectedClaim(claim, "Handle resolves to a non-live (seed/synthetic) artifact and cannot appear on a live surface."));
                continue;
            }

            accepted.Add(claim);
        }

        return new ClaimGuardOutcome(accepted, rejected);
    }
}