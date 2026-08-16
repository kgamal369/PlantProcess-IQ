using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace PlantProcess.Application.Assistant.Serving;

/// <summary>
/// T-137. WHAT A SERVING RUNTIME IS ALLOWED TO SEND, AND WHAT IT MUST PROVE ABOUT
/// WHAT CAME BACK.
///
/// The obsolete assumption this replaces was that serving a model means posting to one
/// provider's endpoint in one provider's shape. That is wrong for this product, whose
/// customers run self-hosted models, private endpoints and models they bring
/// themselves. Transport is therefore a seam and the provider is a declared
/// descriptor, not a constant baked into a call.
///
/// TWO INVARIANTS THIS FILE EXISTS TO CARRY.
///
/// The payload is minimum-scoped. What leaves the process is the evidence that was
/// actually packed and nothing else: no tenant identity, no permission context, no
/// omitted evidence, no counts describing what a caller was not allowed to see. An
/// endpoint a customer does not control must not receive information the caller could
/// not have seen, and the way to guarantee that is to never assemble it.
///
/// Identity is governed on both sides. A request names the provider and the model
/// release it expects; a response that names a different one is refused rather than
/// used. A model silently swapped underneath an answer is the failure that cannot be
/// detected later from the answer alone.
/// </summary>
public enum ServingOutcome
{
    /// <summary>The approved endpoint answered, and the identity checked out.</summary>
    Completed = 0,

    /// <summary>The runtime declined before calling anything, for a stated reason.</summary>
    Refused = 1,

    /// <summary>The declared budget elapsed. Not an answer and not a refusal.</summary>
    TimedOut = 2,

    /// <summary>The caller cancelled. Not a failure of anything.</summary>
    Cancelled = 3,

    /// <summary>The call did not complete. Never a conclusion about anything.</summary>
    TransportFailed = 4
}

/// <summary>Why a runtime refused. One code per reason, never merged.</summary>
public enum ServingRefusalCode
{
    None = 0,
    EndpointNotApproved = 1,
    ProviderIdentityMissing = 2,
    ModelReleaseIdentityMissing = 3,
    FallbackNotApproved = 4,
    ResponseIdentityMismatch = 5,
    PayloadExceedsDeclaredScope = 6,
    NoEndpointConfigured = 7,
    BudgetNotDeclared = 8
}

/// <summary>How a governed payload reaches an endpoint. Declared, never assumed.</summary>
public enum TransportStyle
{
    /// <summary>A request-and-response exchange over HTTP, shape supplied by the adapter.</summary>
    HttpRequestResponse = 0,

    /// <summary>An in-process runtime. A self-hosted model on the same machine.</summary>
    InProcess = 1,

    /// <summary>A streaming exchange. Declared here; not exercised by this task.</summary>
    Streaming = 2
}

/// <summary>
/// Who is answering, and with which release.
///
/// Both halves are required. A provider without a release identity cannot be held to
/// an answer it gave last month, and a release without a provider cannot be traced at
/// all.
/// </summary>
public sealed record ModelIdentity(string ProviderId, string ModelReleaseId)
{
    public static ModelIdentity Of(string providerId, string modelReleaseId) =>
        new(providerId, modelReleaseId);

    public bool IsComplete =>
        !string.IsNullOrWhiteSpace(ProviderId) && !string.IsNullOrWhiteSpace(ModelReleaseId);

    public bool Matches(ModelIdentity other) =>
        other is not null
        && string.Equals(ProviderId, other.ProviderId, StringComparison.Ordinal)
        && string.Equals(ModelReleaseId, other.ModelReleaseId, StringComparison.Ordinal);

    public override string ToString() => $"{ProviderId}/{ModelReleaseId}";
}

/// <summary>
/// One approved endpoint.
///
/// Endpoint is an opaque locator: a URL, a socket, a local path. This layer never
/// parses it, because the shape belongs to the transport and not to the governance.
/// </summary>
public sealed record ModelEndpointDescriptor(
    string EndpointId,
    ModelIdentity Identity,
    TransportStyle Transport,
    string Locator,
    bool IsSelfHosted,
    ImmutableArray<string> ApprovedFallbackEndpointIds)
{
    public static ModelEndpointDescriptor Create(
        string endpointId,
        ModelIdentity identity,
        TransportStyle transport,
        string locator,
        bool isSelfHosted = false,
        params string[] approvedFallbackEndpointIds)
    {
        if (string.IsNullOrWhiteSpace(endpointId))
        {
            throw new ArgumentException("An endpoint must carry an identifier.", nameof(endpointId));
        }

        return new ModelEndpointDescriptor(
            endpointId,
            identity,
            transport,
            locator,
            isSelfHosted,
            approvedFallbackEndpointIds
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToImmutableArray());
    }
}

/// <summary>
/// One piece of evidence as it will leave the process.
///
/// Handle and payload only. Whatever else the evidence pack knew about permission,
/// ranking or omission stays behind.
/// </summary>
public sealed record ScopedEvidence(string EvidenceHandle, string Payload);

/// <summary>The declared time budget. A runtime without one does not run.</summary>
public sealed record ServingBudget(TimeSpan Timeout)
{
    public static ServingBudget Of(TimeSpan timeout)
    {
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentException(
                "A serving budget must be positive. A runtime with no bound waits forever, "
                    + "and a call that never returns is worse than one that refuses.",
                nameof(timeout));
        }

        return new ServingBudget(timeout);
    }
}

/// <summary>
/// Everything that leaves the process, and nothing else.
///
/// There is deliberately no tenant, no caller role, no permission context, no omitted
/// evidence and no count of anything filtered. A field that does not exist cannot be
/// sent by accident.
/// </summary>
public sealed record ModelInvocationRequest(
    string RequestId,
    ModelIdentity ExpectedIdentity,
    string IntentCode,
    string Language,
    ImmutableArray<ScopedEvidence> Evidence,
    ServingBudget Budget)
{
    /// <summary>Total characters of evidence, for scope checks and cost telemetry.</summary>
    public int EvidenceCharacterCount => Evidence.Sum(e => e.Payload.Length);

    /// <summary>
    /// The governed body, in a transport-neutral form.
    ///
    /// The adapter renders this; no provider shape is assumed here.
    /// </summary>
    public string CanonicalBody()
    {
        var builder = new StringBuilder();
        builder.Append("intent=").Append(IntentCode).Append('\n');
        builder.Append("language=").Append(Language).Append('\n');

        foreach (var evidence in Evidence)
        {
            builder.Append("evidence[").Append(evidence.EvidenceHandle).Append("]=");
            builder.Append(evidence.Payload).Append('\n');
        }

        return builder.ToString();
    }
}

/// <summary>What a runtime returns. Never a bare string.</summary>
public sealed record ModelInvocationResult(
    ServingOutcome Outcome,
    ServingRefusalCode RefusalCode,
    string? AnswerText,
    ModelIdentity? RespondingIdentity,
    string EndpointIdUsed,
    double ElapsedMilliseconds,
    string Reason)
{
    public bool IsAnswer => Outcome == ServingOutcome.Completed && AnswerText is not null;

    public static ModelInvocationResult Refusal(ServingRefusalCode code, string reason) =>
        new(ServingOutcome.Refused, code, null, null, string.Empty, 0.0, reason);
}

/// <summary>A transport-neutral call, built by the adapter and carried by the seam.</summary>
public sealed record TransportRequest(
    string EndpointId,
    string Locator,
    TransportStyle Style,
    ImmutableArray<KeyValuePair<string, string>> Headers,
    string Body);

/// <summary>What a transport returns. Identity is claimed here and checked above.</summary>
public sealed record TransportResponse(
    bool Completed,
    string? Body,
    ModelIdentity? ClaimedIdentity,
    string Diagnostic);
