// Fact Evidence Authority resolver kernel.
//
// Backlog origin: T-218.
//
// Resolves who may state a named fact at a given moment, from declarations alone. It
// never ranks sources against each other, never promotes a supporting source when the
// primary is silent, and never reports absence as disagreement.
//
// Every path out of this file is a resolved authority or a refusal carrying a code.
// Conflict is not among them: comparing two authorities is a downstream judgement, and
// this kernel stops at establishing who has standing.
using System;
using System.Collections.Generic;
using System.Linq;

namespace PlantProcess.Analytics.Core.Kernel;

public static class FactEvidenceAuthorityKernel
{
    /// <summary>
    /// What a source is to a fact at a moment. Undeclared means Irrelevant: a source has
    /// no standing on a fact until somebody says it does.
    /// </summary>
    public static EvidenceRole RoleOf(
        FactEvidenceAuthorityRegistry registry,
        string? factKey,
        string? sourceKey,
        DateTimeOffset asOf)
    {
        ArgumentNullException.ThrowIfNull(registry);

        if (!DeclaredKey.TryNormalise(sourceKey, out var source)) return EvidenceRole.Irrelevant;

        var binding = registry
            .BindingsAt(factKey, asOf)
            .FirstOrDefault(b => string.Equals(b.SourceKey, source, StringComparison.Ordinal));

        return binding?.Role ?? EvidenceRole.Irrelevant;
    }

    /// <summary>
    /// Whether a source may be asked about a fact at all. Answering with the reason is
    /// more useful than answering with nothing.
    /// </summary>
    public static FactAuthorityResolution CheckSourceStanding(
        FactEvidenceAuthorityRegistry registry,
        string? factKey,
        string? sourceKey,
        DateTimeOffset asOf)
    {
        ArgumentNullException.ThrowIfNull(registry);

        if (!registry.TryGetFact(factKey, out _))
        {
            return Refuse(FactAuthorityCodes.FactDeclarationAbsent);
        }

        if (RoleOf(registry, factKey, sourceKey, asOf) == EvidenceRole.Irrelevant)
        {
            return Refuse(FactAuthorityCodes.SourceIrrelevantForFact);
        }

        return new FactAuthorityResolution(
            IsResolved: true,
            Authority: null,
            FactAuthorityCodes.AuthorityResolved,
            TerminalState.Finding,
            ExclusionAttribution.None);
    }

    /// <summary>
    /// Resolve the authority for a fact at a moment, given the evidence actually on
    /// offer. Evidence is admitted only from sources with declared standing, and only
    /// when it clears the fact's declared quality floor.
    /// </summary>
    public static FactAuthorityResolution Resolve(
        FactEvidenceAuthorityRegistry registry,
        string? factKey,
        DateTimeOffset asOf,
        IReadOnlyList<OfferedEvidence> offered)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(offered);

        if (!registry.TryGetFact(factKey, out var fact) || fact is null)
        {
            return Refuse(FactAuthorityCodes.FactDeclarationAbsent);
        }

        var bindings = registry.BindingsAt(fact.FactKey, asOf);

        var primaries = bindings.Where(b => b.Role == EvidenceRole.Primary).ToArray();

        if (primaries.Length == 0)
        {
            // Nobody has been given standing to state this. A supporting source does not
            // inherit the vacancy.
            return Refuse(FactAuthorityCodes.PrimaryAuthorityNotDeclared);
        }

        if (primaries.Length > 1)
        {
            // Two primaries at one moment. There is no global ranking to break the tie
            // with, and inventing one here would be the defect this contract exists to
            // prevent.
            return Refuse(FactAuthorityCodes.AmbiguousPrimaryAuthority);
        }

        var primary = primaries[0];

        var fromPrimary = offered
            .Where(e => e is not null
                     && string.Equals(Normalise(e.FactKey), fact.FactKey, StringComparison.Ordinal)
                     && string.Equals(Normalise(e.SourceKey), primary.SourceKey, StringComparison.Ordinal))
            .ToArray();

        if (fromPrimary.Length == 0)
        {
            // The authority is declared and silent. That is an availability result, not a
            // disagreement, and nothing downstream may read it as one.
            return Refuse(FactAuthorityCodes.PrimaryAuthorityUnavailable);
        }

        if (fromPrimary.All(e => e.Quality < fact.QualityFloor))
        {
            // Present but not good enough. Accepting it because it is all there is would
            // make the floor decorative.
            return Refuse(FactAuthorityCodes.InsufficientEvidenceQuality);
        }

        var admitted = offered
            .Where(e => e is not null && string.Equals(Normalise(e.FactKey), fact.FactKey, StringComparison.Ordinal))
            .Where(e => e.Quality >= fact.QualityFloor)
            .Select(e => Normalise(e.SourceKey))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var supporting = SourcesInRole(bindings, admitted, EvidenceRole.Supporting);
        var corroborating = SourcesInRole(bindings, admitted, EvidenceRole.Corroborating);

        return new FactAuthorityResolution(
            IsResolved: true,
            new ResolvedFactAuthority(fact.FactKey, primary.SourceKey, supporting, corroborating, asOf),
            FactAuthorityCodes.AuthorityResolved,
            TerminalState.Finding,
            ExclusionAttribution.None);
    }

    private static IReadOnlyList<string> SourcesInRole(
        IReadOnlyList<FactSourceAuthorityDeclaration> bindings,
        IReadOnlyList<string> admittedSources,
        EvidenceRole role) =>
        bindings
            .Where(b => b.Role == role && admittedSources.Contains(b.SourceKey, StringComparer.Ordinal))
            .Select(b => b.SourceKey)
            .OrderBy(k => k, StringComparer.Ordinal)
            .ToArray();

    private static string Normalise(string? key) =>
        DeclaredKey.TryNormalise(key, out var normalised) ? normalised : string.Empty;

    private static FactAuthorityResolution Refuse(string code) =>
        new(IsResolved: false,
            Authority: null,
            code,
            TerminalState.RefusedByGuard,
            ExclusionAttribution.Declaration);
}