using System;
using System.Collections.Immutable;
using System.Linq;
using PlantProcess.Application.Assistant.Retrieval;

namespace PlantProcess.Application.Assistant.Serving;

/// <summary>
/// T-137. BUILDING WHAT LEAVES THE PROCESS.
///
/// This is the only way to construct a <see cref="ModelInvocationRequest"/> from an
/// evidence pack, and it copies exactly two fields per item: the handle and the
/// payload.
///
/// WHY A BUILDER RATHER THAN A MAPPING. An endpoint may be a customer's own machine,
/// a private deployment or a model they brought themselves. Whatever it is, it must
/// not receive anything the caller could not have seen. The pack carries several
/// things that fit that description: the permitted-candidate count, the omitted
/// evidence and its reasons, the truncation flag, the fingerprint computed over the
/// permitted set. None of them is secret on its own and all of them describe the
/// shape of what a caller was allowed to see.
///
/// So the request type has no field for them, and this builder has no path to one.
/// Omitted evidence is not sent because it is not read; the pack's counts are not sent
/// because there is nowhere to put them.
///
/// A PACK THAT IS NOT AN ANSWER IS NOT SENT AT ALL. A refused, unavailable or
/// unexecutable pack has nothing to ground an answer in, and calling a model with an
/// empty payload would invite one to answer from its own memory. That is the failure
/// this whole chain exists to prevent, arriving one layer from the end.
/// </summary>
public static class ScopedPayloadBuilder
{
    /// <summary>Build the request, or explain why the pack cannot be served.</summary>
    public static (ModelInvocationRequest? Request, ServingRefusalCode Refusal, string Reason) Build(
        string requestId,
        ModelIdentity expectedIdentity,
        EvidencePack pack,
        string language,
        ServingBudget budget)
    {
        ArgumentNullException.ThrowIfNull(pack);
        ArgumentNullException.ThrowIfNull(budget);
        ArgumentNullException.ThrowIfNull(expectedIdentity);

        if (!expectedIdentity.IsComplete)
        {
            return (
                null,
                string.IsNullOrWhiteSpace(expectedIdentity.ProviderId)
                    ? ServingRefusalCode.ProviderIdentityMissing
                    : ServingRefusalCode.ModelReleaseIdentityMissing,
                "A request must name both the provider and the model release it expects. "
                    + "An answer that cannot be traced to a release cannot be defended later.");
        }

        if (pack.Outcome != RetrievalOutcome.EvidencePacked || pack.Items.IsEmpty)
        {
            return (
                null,
                ServingRefusalCode.PayloadExceedsDeclaredScope,
                $"The evidence pack is '{pack.Outcome}' with {pack.Items.Length} item(s). "
                    + "There is nothing to ground an answer in, and calling a model with an "
                    + "empty payload invites it to answer from its own memory.");
        }

        // Exactly two fields per packed item. Everything else the pack knows stays here.
        var evidence = pack.Items
            .OrderBy(item => item.Rank)
            .Select(item => new ScopedEvidence(item.EvidenceHandle, item.Payload))
            .ToImmutableArray();

        var request = new ModelInvocationRequest(
            requestId,
            expectedIdentity,
            pack.IntentCode,
            language,
            evidence,
            budget);

        return (request, ServingRefusalCode.None, "Minimum-scoped payload built from "
            + $"{evidence.Length} packed item(s).");
    }

    /// <summary>
    /// Fields that must never appear in a rendered body.
    ///
    /// Named here so a test can assert their absence rather than a reviewer having to
    /// notice them. This is a check on the rendering, not a substitute for the type
    /// having no field for them in the first place.
    /// </summary>
    public static ImmutableArray<string> ForbiddenPayloadTokens =>
        ImmutableArray.Create(
            "tenant",
            "callerRole",
            "permitted",
            "omitted",
            "truncated",
            "rejectedByPermission",
            "PackFingerprint");
}
