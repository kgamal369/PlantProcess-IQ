using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PlantProcess.Application.Assistant.Serving;

/// <summary>The seam every transport implements. No provider shape reaches above it.</summary>
public interface IModelTransport
{
    string TransportId { get; }

    Task<TransportResponse> SendAsync(TransportRequest request, CancellationToken cancellationToken);
}

/// <summary>The replaceable runtime. One method, one governed result.</summary>
public interface IModelServingRuntime
{
    string RuntimeId { get; }

    Task<ModelInvocationResult> InvokeAsync(
        ModelInvocationRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// The approved endpoint set.
///
/// An endpoint the operator has not approved does not exist as far as this runtime is
/// concerned, and a fallback is approved for a specific primary rather than approved
/// in general.
/// </summary>
public sealed record ServingPolicy(
    ImmutableArray<ModelEndpointDescriptor> ApprovedEndpoints,
    string PrimaryEndpointId)
{
    public static ServingPolicy Of(string primaryEndpointId, params ModelEndpointDescriptor[] endpoints)
    {
        var duplicates = endpoints
            .GroupBy(e => e.EndpointId, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToArray();

        if (duplicates.Length > 0)
        {
            throw new ArgumentException(
                "An approved endpoint set may not declare the same identifier twice: "
                    + string.Join(", ", duplicates));
        }

        return new ServingPolicy(
            endpoints.OrderBy(e => e.EndpointId, StringComparer.Ordinal).ToImmutableArray(),
            primaryEndpointId);
    }

    public ModelEndpointDescriptor? Find(string endpointId) =>
        ApprovedEndpoints.FirstOrDefault(e =>
            string.Equals(e.EndpointId, endpointId, StringComparison.Ordinal));

    public bool IsApproved(string endpointId) => Find(endpointId) is not null;

    /// <summary>
    /// Whether this fallback is approved FOR THIS PRIMARY.
    ///
    /// Approval is a relation, not a property. An endpoint approved for one primary is
    /// not thereby approved as the stand-in for another, because the operator who
    /// approved it was answering a narrower question.
    /// </summary>
    public bool IsApprovedFallbackFor(string primaryEndpointId, string fallbackEndpointId)
    {
        var primary = Find(primaryEndpointId);
        return primary is not null
            && primary.ApprovedFallbackEndpointIds.Contains(fallbackEndpointId, StringComparer.Ordinal);
    }
}

/// <summary>
/// T-137. THE GOVERNED SERVING RUNTIME.
///
/// THE ORDER OF THE WORK.
///
///     policy  ->  identity  ->  budget  ->  transport  ->  identity again
///
/// Policy first: an unapproved endpoint is never contacted, so a misconfiguration
/// cannot become a call to a machine nobody approved.
///
/// Identity is checked twice, and the second time is the one that matters. Asking for
/// a release proves nothing; a response claiming a different release is refused rather
/// than used, because a model silently swapped underneath an answer cannot be detected
/// afterwards from the answer alone.
///
/// FALLBACK IS NOT A RECOVERY STRATEGY HERE. When the primary fails, the runtime tries
/// only endpoints the operator approved as fallbacks FOR THAT PRIMARY. If none is
/// approved, it refuses and says so. Answering from an unapproved endpoint would mean
/// a customer's question left for a machine they never agreed to, which is worse than
/// no answer.
///
/// A TIMEOUT IS NOT AN ANSWER. Nor is a cancellation, nor a transport failure. Each is
/// its own outcome, and none of them carries text that a caller could mistake for a
/// conclusion.
/// </summary>
public sealed class GovernedModelServingRuntime : IModelServingRuntime
{
    private readonly ServingPolicy _policy;
    private readonly IModelTransport _transport;

    public GovernedModelServingRuntime(ServingPolicy policy, IModelTransport transport)
    {
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
    }

    public string RuntimeId => "governed/1";

    /// <summary>Every endpoint this runtime actually contacted, in order. For audit and test.</summary>
    public ImmutableArray<string> ContactedEndpointIds => _contacted.ToImmutableArray();

    private readonly List<string> _contacted = new();

    public async Task<ModelInvocationResult> InvokeAsync(
        ModelInvocationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Budget is null)
        {
            return ModelInvocationResult.Refusal(
                ServingRefusalCode.BudgetNotDeclared,
                "No time budget was declared. A runtime with no bound waits forever.");
        }

        if (!request.ExpectedIdentity.IsComplete)
        {
            return ModelInvocationResult.Refusal(
                string.IsNullOrWhiteSpace(request.ExpectedIdentity.ProviderId)
                    ? ServingRefusalCode.ProviderIdentityMissing
                    : ServingRefusalCode.ModelReleaseIdentityMissing,
                "The request does not name both a provider and a model release.");
        }

        var primary = _policy.Find(_policy.PrimaryEndpointId);
        if (primary is null)
        {
            return ModelInvocationResult.Refusal(
                ServingRefusalCode.NoEndpointConfigured,
                $"The policy names '{_policy.PrimaryEndpointId}' as primary and does not "
                    + "approve it. Nothing is contacted.");
        }

        var attempt = await AttemptAsync(primary, request, cancellationToken).ConfigureAwait(false);
        if (attempt.Outcome is ServingOutcome.Completed or ServingOutcome.Cancelled
            or ServingOutcome.TimedOut or ServingOutcome.Refused)
        {
            return attempt;
        }

        // The primary did not complete. Only endpoints approved for THIS primary may be
        // tried, and an empty approved set is a refusal rather than a search.
        foreach (var fallbackId in primary.ApprovedFallbackEndpointIds)
        {
            if (!_policy.IsApprovedFallbackFor(primary.EndpointId, fallbackId))
            {
                continue;
            }

            var fallback = _policy.Find(fallbackId);
            if (fallback is null)
            {
                continue;
            }

            var second = await AttemptAsync(fallback, request, cancellationToken).ConfigureAwait(false);
            if (second.Outcome != ServingOutcome.TransportFailed)
            {
                return second;
            }
        }

        return new ModelInvocationResult(
            ServingOutcome.Refused,
            ServingRefusalCode.FallbackNotApproved,
            null,
            null,
            primary.EndpointId,
            attempt.ElapsedMilliseconds,
            $"Endpoint '{primary.EndpointId}' did not complete and no approved fallback "
                + "answered. No unapproved endpoint is contacted, because a question sent "
                + "to a machine the customer never approved is worse than no answer.");
    }

    private async Task<ModelInvocationResult> AttemptAsync(
        ModelEndpointDescriptor endpoint,
        ModelInvocationRequest request,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        _contacted.Add(endpoint.EndpointId);

        using var budgeted = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budgeted.CancelAfter(request.Budget.Timeout);

        TransportResponse response;
        try
        {
            response = await _transport
                .SendAsync(ModelGatewayAdapter.Translate(endpoint, request), budgeted.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();

            // The caller cancelling and the budget elapsing are different events and
            // are reported as such, however similar they look from inside.
            var cancelledByCaller = cancellationToken.IsCancellationRequested;
            return new ModelInvocationResult(
                cancelledByCaller ? ServingOutcome.Cancelled : ServingOutcome.TimedOut,
                ServingRefusalCode.None,
                null,
                null,
                endpoint.EndpointId,
                stopwatch.Elapsed.TotalMilliseconds,
                cancelledByCaller
                    ? "The caller cancelled the request. No answer was produced and none is implied."
                    : $"The declared budget of {request.Budget.Timeout.TotalMilliseconds:0} ms "
                        + "elapsed. A timeout is not an answer and is never rendered as one.");
        }
        catch (Exception failure)
        {
            stopwatch.Stop();
            return new ModelInvocationResult(
                ServingOutcome.TransportFailed,
                ServingRefusalCode.None,
                null,
                null,
                endpoint.EndpointId,
                stopwatch.Elapsed.TotalMilliseconds,
                $"The transport failed: {failure.GetType().Name}. This is a failure to "
                    + "execute and is never a conclusion.");
        }

        stopwatch.Stop();

        if (!response.Completed)
        {
            return new ModelInvocationResult(
                ServingOutcome.TransportFailed,
                ServingRefusalCode.None,
                null,
                null,
                endpoint.EndpointId,
                stopwatch.Elapsed.TotalMilliseconds,
                $"The endpoint did not complete: {response.Diagnostic}");
        }

        // The check that cannot be made later. A response claiming a different release
        // is refused, not used.
        if (response.ClaimedIdentity is null || !request.ExpectedIdentity.Matches(response.ClaimedIdentity))
        {
            return new ModelInvocationResult(
                ServingOutcome.Refused,
                ServingRefusalCode.ResponseIdentityMismatch,
                null,
                response.ClaimedIdentity,
                endpoint.EndpointId,
                stopwatch.Elapsed.TotalMilliseconds,
                $"The request expected {request.ExpectedIdentity} and the response claims "
                    + $"{response.ClaimedIdentity?.ToString() ?? "no identity"}. A model "
                    + "swapped underneath an answer cannot be detected from the answer.");
        }

        return new ModelInvocationResult(
            ServingOutcome.Completed,
            ServingRefusalCode.None,
            response.Body,
            response.ClaimedIdentity,
            endpoint.EndpointId,
            stopwatch.Elapsed.TotalMilliseconds,
            $"Endpoint '{endpoint.EndpointId}' answered as {response.ClaimedIdentity}.");
    }
}

/// <summary>
/// T-137. THE MODEL GATEWAY ADAPTER.
///
/// It turns a governed request into a transport-neutral call. It assumes no verb, no
/// path and no provider body shape, which is the assumption this task exists to
/// remove: a self-hosted runtime on a socket and a private HTTP deployment are both
/// first-class here, and neither is a special case of the other.
/// </summary>
public static class ModelGatewayAdapter
{
    public static TransportRequest Translate(
        ModelEndpointDescriptor endpoint, ModelInvocationRequest request)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(request);

        // Headers carry identity and correlation, never tenancy or permission.
        var headers = ImmutableArray.Create(
            new KeyValuePair<string, string>("x-ppiq-request-id", request.RequestId),
            new KeyValuePair<string, string>("x-ppiq-expected-provider", request.ExpectedIdentity.ProviderId),
            new KeyValuePair<string, string>("x-ppiq-expected-release", request.ExpectedIdentity.ModelReleaseId));

        return new TransportRequest(
            endpoint.EndpointId,
            endpoint.Locator,
            endpoint.Transport,
            headers,
            request.CanonicalBody());
    }
}
