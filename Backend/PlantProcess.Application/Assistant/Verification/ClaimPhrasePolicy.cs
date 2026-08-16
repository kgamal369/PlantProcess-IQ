using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using PlantProcess.Application.Assistant.Planning;

namespace PlantProcess.Application.Assistant.Verification;

/// <summary>
/// T-181. THE PHRASE POLICY. Declared, deterministic, and free of any plant vocabulary.
///
/// A claim class comes from the evidence that produced it. Language cannot raise it.
/// An association phrased as a cause, a learned contribution phrased as a proven root
/// cause, a prediction phrased as a certainty and a remediation candidate phrased as
/// an instruction are the four upgrades that matter, and each is refused here.
///
/// WHY A TABLE AND NOT A MODEL. Asking a language model whether language overreaches
/// would make the verifier's most credibility-critical judgement depend on the same
/// kind of component it exists to check. A declared phrase table is weaker at nuance
/// and stronger where it counts: it is inspectable, reproducible, and cannot be
/// argued into a different answer on a second run.
///
/// WHAT THIS DELIBERATELY IS NOT. It contains no plant-specific or domain-specific
/// causal rule. The phrases are about the strength of a claim, not about any process,
/// so the policy is the same for every industry the product serves.
///
/// THE LANGUAGES ARE FIXTURE-DECLARED. Adding one is adding a row. This task does not
/// expand which languages the product supports.
/// </summary>
public static class ClaimPhrasePolicy
{
    public const string DefaultLanguage = "en";

    /// <summary>Languages this policy carries phrases for. Fixture-declared, not aspirational.</summary>
    public static ImmutableArray<string> DeclaredLanguages =>
        ImmutableArray.Create("en", "de");

    /// <summary>One forbidden phrase, and the authority it would wrongly assert.</summary>
    public sealed record ForbiddenPhrase(string Language, string Phrase, string AssertedAuthority);

    /// <summary>
    /// Phrases that assert causation. Forbidden for every class weaker than a
    /// measured causal effect.
    /// </summary>
    private static readonly ImmutableArray<ForbiddenPhrase> CausalPhrases =
        ImmutableArray.Create(
            new ForbiddenPhrase("en", "causes", "causation"),
            new ForbiddenPhrase("en", "caused by", "causation"),
            new ForbiddenPhrase("en", "is due to", "causation"),
            new ForbiddenPhrase("en", "because of", "causation"),
            new ForbiddenPhrase("en", "root cause", "causation"),
            new ForbiddenPhrase("en", "driven by", "causation"),
            new ForbiddenPhrase("en", "results in", "causation"),
            new ForbiddenPhrase("de", "verursacht", "causation"),
            new ForbiddenPhrase("de", "ursache", "causation"),
            new ForbiddenPhrase("de", "wegen", "causation"));

    /// <summary>Phrases that assert certainty. Forbidden for an estimate.</summary>
    private static readonly ImmutableArray<ForbiddenPhrase> CertaintyPhrases =
        ImmutableArray.Create(
            new ForbiddenPhrase("en", "will certainly", "certainty"),
            new ForbiddenPhrase("en", "guarantees", "certainty"),
            new ForbiddenPhrase("en", "is guaranteed", "certainty"),
            new ForbiddenPhrase("en", "definitely will", "certainty"),
            new ForbiddenPhrase("de", "garantiert", "certainty"),
            new ForbiddenPhrase("de", "mit sicherheit", "certainty"));

    /// <summary>Phrases that assert an instruction. Forbidden for a suggestion.</summary>
    private static readonly ImmutableArray<ForbiddenPhrase> InstructionPhrases =
        ImmutableArray.Create(
            new ForbiddenPhrase("en", "you must set", "instruction"),
            new ForbiddenPhrase("en", "set the parameter to", "instruction"),
            new ForbiddenPhrase("en", "apply this change now", "instruction"),
            new ForbiddenPhrase("de", "stellen sie ein", "instruction"));

    /// <summary>Phrases that assert a proven finding. Forbidden for evidence under review.</summary>
    private static readonly ImmutableArray<ForbiddenPhrase> ProofPhrases =
        ImmutableArray.Create(
            new ForbiddenPhrase("en", "proves that", "proof"),
            new ForbiddenPhrase("en", "proven finding", "proof"),
            new ForbiddenPhrase("en", "confirms that", "proof"),
            new ForbiddenPhrase("de", "beweist", "proof"));

    /// <summary>
    /// Which phrase families a claim class may not use.
    ///
    /// A measured causal effect may speak of causation, because that is what it
    /// measured. Nothing weaker may.
    /// </summary>
    public static ImmutableArray<ForbiddenPhrase> ForbiddenFor(ClaimClass claimClass)
    {
        var forbidden = new List<ForbiddenPhrase>();

        if (claimClass != ClaimClass.CausalEffect)
        {
            forbidden.AddRange(CausalPhrases);
        }

        if (claimClass != ClaimClass.ObservedFact)
        {
            forbidden.AddRange(CertaintyPhrases);
            forbidden.AddRange(ProofPhrases);
        }

        forbidden.AddRange(InstructionPhrases);

        return forbidden
            .OrderBy(p => p.Language, StringComparer.Ordinal)
            .ThenBy(p => p.Phrase, StringComparer.Ordinal)
            .ToImmutableArray();
    }

    /// <summary>
    /// Find an upgrade in one claim's asserted text.
    ///
    /// Only the phrases declared for the draft's language are applied, and a language
    /// the policy does not carry is reported rather than silently passing.
    /// </summary>
    public static ImmutableArray<ForbiddenPhrase> Violations(
        ClaimClass claimClass,
        string language,
        string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return ImmutableArray<ForbiddenPhrase>.Empty;
        }

        var lowered = text.ToLowerInvariant();

        return ForbiddenFor(claimClass)
            .Where(p => string.Equals(p.Language, language, StringComparison.OrdinalIgnoreCase))
            .Where(p => lowered.Contains(p.Phrase, StringComparison.Ordinal))
            .ToImmutableArray();
    }

    /// <summary>Whether the policy carries phrases for this language at all.</summary>
    public static bool CarriesLanguage(string language) =>
        DeclaredLanguages.Any(l => string.Equals(l, language, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Words that mark a refusal as still present in the rendered text.
    ///
    /// A governed refusal a model has phrased must still read as a refusal. This is
    /// the minimum a deterministic check can establish without a model, and it is
    /// stated as such rather than as proof that the refusal was phrased well.
    /// </summary>
    public static ImmutableArray<string> RefusalMarkers(string language) =>
        string.Equals(language, "de", StringComparison.OrdinalIgnoreCase)
            ? ImmutableArray.Create("nicht", "keine", "unzureichend", "abgelehnt")
            : ImmutableArray.Create(
                "cannot", "not enough", "insufficient", "no data", "refused",
                "unsupported", "blocked", "unable");

    /// <summary>
    /// Phrases that turn a failure to execute into a statement about the plant.
    ///
    /// These are the sentences a transport failure must never be rendered as.
    /// </summary>
    public static ImmutableArray<string> DomainConclusionPhrases(string language) =>
        string.Equals(language, "de", StringComparison.OrdinalIgnoreCase)
            ? ImmutableArray.Create("kein zusammenhang", "kein risiko", "keine belege")
            : ImmutableArray.Create(
                "no relationship", "no correlation", "no risk", "no evidence exists",
                "there is no link", "nothing was found");
}
