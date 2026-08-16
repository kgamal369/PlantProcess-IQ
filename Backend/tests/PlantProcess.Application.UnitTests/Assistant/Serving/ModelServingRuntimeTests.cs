using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PlantProcess.Application.Assistant.Planning;
using PlantProcess.Application.Assistant.Retrieval;
using PlantProcess.Application.Assistant.Serving;
using Xunit;

namespace PlantProcess.Application.UnitTests.Assistant.Serving;

/// <summary>
/// T-137. THE SERVING CONTRACT, PROVEN AGAINST A FAKE AND A STUB.
///
/// No probe here contacts a real provider, and none claims to. What is established is
/// the contract: what leaves the process, what is refused, and what a response must
/// prove about itself.
/// </summary>
public sealed class ModelServingRuntimeTests
{
    private const string Tenant = "tenant_fixture";
    private const string ToolId = "layer_a.exact_count";
    private const string PrimaryEndpoint = "endpoint_primary";
    private const string ApprovedFallback = "endpoint_approved_fallback";
    private const string UnapprovedEndpoint = "endpoint_never_approved";

    private static void AssertOrdered(IEnumerable<string> expected, IEnumerable<string> actual) =>
        Assert.Equal(expected.ToArray(), actual.ToArray());

    private static ModelIdentity Identity() => ModelIdentity.Of("selfhosted_provider", "release_2026_08");

    private static ModelEndpointDescriptor Primary(params string[] fallbacks) =>
        ModelEndpointDescriptor.Create(
            PrimaryEndpoint, Identity(), TransportStyle.HttpRequestResponse,
            "https://private.endpoint.invalid/v1/infer", isSelfHosted: true, fallbacks);

    private static ServingPolicy Policy(params string[] approvedFallbacks) =>
        ServingPolicy.Of(
            PrimaryEndpoint,
            Primary(approvedFallbacks),
            ModelEndpointDescriptor.Create(
                ApprovedFallback, Identity(), TransportStyle.InProcess, "inproc://local", true),
            ModelEndpointDescriptor.Create(
                UnapprovedEndpoint, Identity(), TransportStyle.HttpRequestResponse,
                "https://someone.elses.invalid/v1/infer"));

    private static EvidencePack Pack(int items = 2, int budgetTokens = 1000)
    {
        var registry = ToolRegistry.Of(DeclaredTool.Create(
            ToolId, ToolLayer.LayerA, ToolExactness.Exact, ClaimClass.ObservedFact, "unit_scope"));

        var plan = DeterministicToolPlanner.Plan(new PlanningRequest(
            PermissionContext.Of(Tenant, "process_engineer", ToolId),
            ResolvedIntent.Create("serving_probe", ClaimClass.ObservedFact, true, "unit_scope"),
            ImmutableArray.Create(ResolvedEntity.Bound("unit_scope", "unit_scope_0001")),
            registry));

        var candidates = Enumerable.Range(0, items)
            .Select(i => EvidenceCandidate.Create(
                $"evidence_{i:0000}", Tenant, ToolId, EvidenceClass.StructuredToolResult,
                $"content_{i:0000}", $"payload of item {i}", 10, exactScore: 0.9 - i * 0.01))
            .ToArray();

        return EvidencePacker.Pack(plan, candidates, TokenBudget.Of(budgetTokens, 200));
    }

    private static ServingBudget Budget(int milliseconds = 5000) =>
        ServingBudget.Of(TimeSpan.FromMilliseconds(milliseconds));

    private static ModelInvocationRequest Request(EvidencePack? pack = null)
    {
        var (request, refusal, reason) = ScopedPayloadBuilder.Build(
            "request_0001", Identity(), pack ?? Pack(), "en", Budget());

        Assert.Equal(ServingRefusalCode.None, refusal);
        Assert.NotNull(request);
        Assert.False(string.IsNullOrWhiteSpace(reason));
        return request!;
    }

    // ============================================ S1  minimum-scoped payload

    [Fact]
    public void S1_ThePayloadCarriesOnlyHandlesAndPayloads()
    {
        var request = Request();

        Assert.Equal(2, request.Evidence.Length);
        AssertOrdered(new[] { "evidence_0000", "evidence_0001" },
            request.Evidence.Select(e => e.EvidenceHandle));
        Assert.All(request.Evidence, e => Assert.StartsWith("payload of item", e.Payload));
    }

    [Fact]
    public void S1_TheRenderedBodyCarriesNoTenantPermissionOrOmissionField()
    {
        var body = Request().CanonicalBody();

        foreach (var forbidden in ScopedPayloadBuilder.ForbiddenPayloadTokens)
        {
            Assert.DoesNotContain(forbidden, body, StringComparison.OrdinalIgnoreCase);
        }

        Assert.DoesNotContain(Tenant, body, StringComparison.Ordinal);
    }

    [Fact]
    public void S1_TheRequestTypeHasNoFieldForTenancyOrPermission()
    {
        // Stronger than checking the rendering: there is nowhere to put it.
        var names = typeof(ModelInvocationRequest)
            .GetProperties()
            .Select(p => p.Name.ToLowerInvariant())
            .ToArray();

        foreach (var forbidden in new[] { "tenant", "role", "permission", "permitted", "omitted", "fingerprint" })
        {
            Assert.DoesNotContain(names, name => name.Contains(forbidden, StringComparison.Ordinal));
        }
    }

    [Fact]
    public void S1_OmittedEvidenceNeverLeavesTheProcess()
    {
        // A pack whose budget forced omissions. What was left out must not appear.
        var pack = Pack(items: 6, budgetTokens: 240);
        Assert.True(pack.Truncated);

        var request = Request(pack);
        var body = request.CanonicalBody();

        foreach (var omitted in pack.Omitted.Select(o => o.EvidenceHandle))
        {
            Assert.DoesNotContain(omitted, body, StringComparison.Ordinal);
        }

        Assert.Equal(pack.Items.Length, request.Evidence.Length);
    }

    [Fact]
    public void S1_APackThatIsNotAnAnswerIsNeverSent()
    {
        var registry = ToolRegistry.Of(DeclaredTool.Create(
            ToolId, ToolLayer.LayerA, ToolExactness.Exact, ClaimClass.ObservedFact, "unit_scope"));

        var plan = DeterministicToolPlanner.Plan(new PlanningRequest(
            PermissionContext.Of(Tenant, "process_engineer", ToolId),
            ResolvedIntent.Create("serving_probe", ClaimClass.ObservedFact, true, "unit_scope"),
            ImmutableArray.Create(ResolvedEntity.Bound("unit_scope", "unit_scope_0001")),
            registry));

        var empty = EvidencePacker.Pack(plan, Array.Empty<EvidenceCandidate>(), TokenBudget.Of(1000, 200));
        var (request, refusal, reason) = ScopedPayloadBuilder.Build(
            "request_empty", Identity(), empty, "en", Budget());

        Assert.Null(request);
        Assert.Equal(ServingRefusalCode.PayloadExceedsDeclaredScope, refusal);
        Assert.Contains("answer from its own memory", reason);
    }

    // ================================================ S2  governed identity

    [Fact]
    public async Task S2_ARequestWithoutAProviderOrReleaseIsRefused()
    {
        var runtime = new GovernedModelServingRuntime(Policy(), new CompletingTransport(Identity()));

        foreach (var incomplete in new[]
        {
            ModelIdentity.Of(string.Empty, "release_2026_08"),
            ModelIdentity.Of("selfhosted_provider", string.Empty)
        })
        {
            var request = Request() with { ExpectedIdentity = incomplete };
            var result = await runtime.InvokeAsync(request, CancellationToken.None);

            Assert.Equal(ServingOutcome.Refused, result.Outcome);
            Assert.Contains(result.RefusalCode, new[]
            {
                ServingRefusalCode.ProviderIdentityMissing,
                ServingRefusalCode.ModelReleaseIdentityMissing
            });
        }
    }

    [Fact]
    public async Task S2_AResponseClaimingADifferentReleaseIsRefused()
    {
        var swapped = ModelIdentity.Of("selfhosted_provider", "release_2026_07");
        var runtime = new GovernedModelServingRuntime(Policy(), new CompletingTransport(swapped));

        var result = await runtime.InvokeAsync(Request(), CancellationToken.None);

        Assert.Equal(ServingOutcome.Refused, result.Outcome);
        Assert.Equal(ServingRefusalCode.ResponseIdentityMismatch, result.RefusalCode);
        Assert.Null(result.AnswerText);
        Assert.Contains("cannot be detected from the answer", result.Reason);
    }

    [Fact]
    public async Task S2_AResponseClaimingNoIdentityIsRefused()
    {
        var runtime = new GovernedModelServingRuntime(Policy(), new CompletingTransport(null));

        var result = await runtime.InvokeAsync(Request(), CancellationToken.None);

        Assert.Equal(ServingRefusalCode.ResponseIdentityMismatch, result.RefusalCode);
        Assert.Null(result.AnswerText);
    }

    [Fact]
    public async Task S2_AMatchingIdentityCompletesAndIsRecorded()
    {
        var runtime = new GovernedModelServingRuntime(Policy(), new CompletingTransport(Identity()));

        var result = await runtime.InvokeAsync(Request(), CancellationToken.None);

        Assert.Equal(ServingOutcome.Completed, result.Outcome);
        Assert.True(result.IsAnswer);
        Assert.Equal(Identity(), result.RespondingIdentity);
        Assert.Equal(PrimaryEndpoint, result.EndpointIdUsed);
    }

    // ============================================ S3  timeout and cancel

    [Fact]
    public async Task S3_ABudgetThatElapsesProducesATimeoutAndNoAnswer()
    {
        var runtime = new GovernedModelServingRuntime(
            Policy(), new SlowTransport(TimeSpan.FromSeconds(30)));

        var request = Request() with { Budget = Budget(60) };
        var result = await runtime.InvokeAsync(request, CancellationToken.None);

        Assert.Equal(ServingOutcome.TimedOut, result.Outcome);
        Assert.Null(result.AnswerText);
        Assert.Contains("not an answer", result.Reason);
    }

    [Fact]
    public async Task S3_ACallerCancellationIsItsOwnOutcome()
    {
        var runtime = new GovernedModelServingRuntime(
            Policy(), new SlowTransport(TimeSpan.FromSeconds(30)));

        using var cancellation = new CancellationTokenSource();
        cancellation.CancelAfter(TimeSpan.FromMilliseconds(50));

        var result = await runtime.InvokeAsync(Request(), cancellation.Token);

        Assert.Equal(ServingOutcome.Cancelled, result.Outcome);
        Assert.Null(result.AnswerText);
        Assert.Contains("none is implied", result.Reason);
    }

    [Fact]
    public void S3_ABudgetOfZeroOrLessIsRefusedAtConstruction()
    {
        Assert.Throws<ArgumentException>(() => ServingBudget.Of(TimeSpan.Zero));
        Assert.Throws<ArgumentException>(() => ServingBudget.Of(TimeSpan.FromSeconds(-1)));
    }

    [Fact]
    public async Task S3_ATimeoutIsNotATransportFailureAndNeitherIsAnAnswer()
    {
        var runtime = new GovernedModelServingRuntime(
            Policy(), new SlowTransport(TimeSpan.FromSeconds(30)));

        var result = await runtime.InvokeAsync(Request() with { Budget = Budget(60) }, CancellationToken.None);

        Assert.NotEqual(ServingOutcome.Completed, result.Outcome);
        Assert.NotEqual(ServingOutcome.TransportFailed, result.Outcome);
        Assert.NotEqual(ServingOutcome.Refused, result.Outcome);
    }

    // ======================================== S4  no unapproved fallback

    [Fact]
    public async Task S4_AnUnapprovedEndpointIsNeverContacted()
    {
        // The primary fails and an unapproved endpoint exists in the policy. It must
        // not be tried, and the spy proves it was not.
        var transport = new FailingThenCompletingTransport(Identity());
        var runtime = new GovernedModelServingRuntime(Policy(), transport);

        var result = await runtime.InvokeAsync(Request(), CancellationToken.None);

        Assert.Equal(ServingOutcome.Refused, result.Outcome);
        Assert.Equal(ServingRefusalCode.FallbackNotApproved, result.RefusalCode);
        AssertOrdered(new[] { PrimaryEndpoint }, transport.ContactedEndpointIds);
        Assert.DoesNotContain(UnapprovedEndpoint, transport.ContactedEndpointIds);
    }

    [Fact]
    public async Task S4_AnApprovedFallbackIsTriedAndAnswers()
    {
        var transport = new FailingThenCompletingTransport(Identity());
        var runtime = new GovernedModelServingRuntime(Policy(ApprovedFallback), transport);

        var result = await runtime.InvokeAsync(Request(), CancellationToken.None);

        Assert.Equal(ServingOutcome.Completed, result.Outcome);
        Assert.Equal(ApprovedFallback, result.EndpointIdUsed);
        AssertOrdered(new[] { PrimaryEndpoint, ApprovedFallback }, transport.ContactedEndpointIds);
    }

    [Fact]
    public void S4_ApprovalIsARelationNotAProperty()
    {
        // An endpoint approved as a fallback for one primary is not thereby approved
        // for another, because the operator answered a narrower question.
        var policy = Policy(ApprovedFallback);

        Assert.True(policy.IsApproved(UnapprovedEndpoint));
        Assert.True(policy.IsApprovedFallbackFor(PrimaryEndpoint, ApprovedFallback));
        Assert.False(policy.IsApprovedFallbackFor(PrimaryEndpoint, UnapprovedEndpoint));
        Assert.False(policy.IsApprovedFallbackFor(ApprovedFallback, UnapprovedEndpoint));
    }

    [Fact]
    public async Task S4_APrimaryTheOperatorNeverApprovedContactsNothing()
    {
        var policy = ServingPolicy.Of("endpoint_absent",
            ModelEndpointDescriptor.Create(
                ApprovedFallback, Identity(), TransportStyle.InProcess, "inproc://local"));

        var transport = new CompletingTransport(Identity());
        var runtime = new GovernedModelServingRuntime(policy, transport);

        var result = await runtime.InvokeAsync(Request(), CancellationToken.None);

        Assert.Equal(ServingRefusalCode.NoEndpointConfigured, result.RefusalCode);
        Assert.Empty(transport.ContactedEndpointIds);
    }

    [Fact]
    public void S4_APolicyMayNotDeclareTheSameEndpointTwice()
    {
        var endpoint = ModelEndpointDescriptor.Create(
            PrimaryEndpoint, Identity(), TransportStyle.InProcess, "inproc://local");

        Assert.Throws<ArgumentException>(() => ServingPolicy.Of(PrimaryEndpoint, endpoint, endpoint));
    }

    // =============================================== S5  transport neutrality

    [Fact]
    public void S5_TheAdapterAssumesNoVerbPathOrProviderBodyShape()
    {
        var endpoint = ModelEndpointDescriptor.Create(
            "endpoint_inproc", Identity(), TransportStyle.InProcess, "inproc://local/model");

        var translated = ModelGatewayAdapter.Translate(endpoint, Request());

        Assert.Equal(TransportStyle.InProcess, translated.Style);
        Assert.Equal("inproc://local/model", translated.Locator);
        Assert.DoesNotContain("POST", translated.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"messages\"", translated.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void S5_TheHeadersCarryIdentityAndCorrelationAndNothingAboutTenancy()
    {
        var translated = ModelGatewayAdapter.Translate(Primary(), Request());
        var names = translated.Headers.Select(h => h.Key).ToArray();

        Assert.Contains("x-ppiq-expected-provider", names);
        Assert.Contains("x-ppiq-expected-release", names);
        Assert.DoesNotContain(names, name => name.Contains("tenant", StringComparison.OrdinalIgnoreCase));
        Assert.All(translated.Headers, header =>
            Assert.DoesNotContain(Tenant, header.Value, StringComparison.Ordinal));
    }

    [Fact]
    public async Task S5_ASelfHostedInProcessEndpointIsAFirstClassCase()
    {
        var policy = ServingPolicy.Of(
            "endpoint_inproc",
            ModelEndpointDescriptor.Create(
                "endpoint_inproc", Identity(), TransportStyle.InProcess, "inproc://local", true));

        var result = await new GovernedModelServingRuntime(policy, new CompletingTransport(Identity()))
            .InvokeAsync(Request(), CancellationToken.None);

        Assert.Equal(ServingOutcome.Completed, result.Outcome);
    }

    // ==================================================== S6  transport stub

    [Fact]
    public async Task S6_AnHttpShapedStubReceivesTheGovernedBodyUnaltered()
    {
        var stub = new HttpTranslatingStub(Identity());
        var runtime = new GovernedModelServingRuntime(Policy(), stub);
        var request = Request();

        var result = await runtime.InvokeAsync(request, CancellationToken.None);

        Assert.Equal(ServingOutcome.Completed, result.Outcome);
        Assert.Equal(request.CanonicalBody(), stub.LastBody);
        Assert.Equal("https://private.endpoint.invalid/v1/infer", stub.LastLocator);
        Assert.Equal(TransportStyle.HttpRequestResponse, stub.LastStyle);
    }

    [Fact]
    public async Task S6_ATransportThatFailsIsNeverAConclusion()
    {
        var runtime = new GovernedModelServingRuntime(Policy(), new ThrowingTransport());

        var result = await runtime.InvokeAsync(Request(), CancellationToken.None);

        Assert.NotEqual(ServingOutcome.Completed, result.Outcome);
        Assert.Null(result.AnswerText);
    }

    // ============================================== S7  readiness honesty

    [Fact]
    public void S7_AnIsolatedImplementationAttainsExactlyOneOfFourStates()
    {
        var report = ServingReadiness.ForIsolatedImplementation(contractTestsPass: true);

        AssertOrdered(
            new[] { "ImplementationGreen" },
            report.AttainedStates.Select(s => s.ToString()));

        Assert.False(report.IsAttained(ServingReadinessState.RuntimeStarted));
        Assert.False(report.IsAttained(ServingReadinessState.BenchmarkMeasured));
        Assert.False(report.IsAttained(ServingReadinessState.ProductionCertified));
        Assert.False(report.IsProductionCertified);
    }

    [Fact]
    public void S7_EachUnattainedStateNamesItsReasonAndItsOwner()
    {
        var report = ServingReadiness.ForIsolatedImplementation(true);

        foreach (var entry in report.Entries.Where(e => !e.Attained))
        {
            Assert.False(string.IsNullOrWhiteSpace(entry.Evidence));
            Assert.False(string.IsNullOrWhiteSpace(entry.OwnerTaskWhenNotAttained));
        }

        var runtime = report.Entries.Single(e => e.State == ServingReadinessState.RuntimeStarted);
        Assert.Contains("prove nothing about a runtime", runtime.Evidence);
        Assert.Contains("T-138", runtime.OwnerTaskWhenNotAttained);
    }

    [Fact]
    public void S7_AFakeAndAStubNeverEstablishThatARuntimeStarted()
    {
        // The distinction this task was told to keep. Passing probes against a fake is
        // ImplementationGreen and is not RuntimeStarted.
        var report = ServingReadiness.ForIsolatedImplementation(true);

        Assert.True(report.IsAttained(ServingReadinessState.ImplementationGreen));
        Assert.False(report.IsAttained(ServingReadinessState.RuntimeStarted));
    }

    [Fact]
    public void S7_BenchmarkMeasuredStatesThatTheGatesAreStillUnavailable()
    {
        var benchmark = ServingReadiness.ForIsolatedImplementation(true)
            .Entries.Single(e => e.State == ServingReadinessState.BenchmarkMeasured);

        Assert.False(benchmark.Attained);
        Assert.Contains("CapabilityUnavailable", benchmark.Evidence);
        Assert.Contains("indistinguishable from a real one", benchmark.Evidence);
    }

    [Fact]
    public void S7_ProductionCertifiedIsTrueOnlyWhenAllFourAre()
    {
        Assert.False(ServingReadiness.Describe(true, true, true, false).IsProductionCertified);
        Assert.False(ServingReadiness.Describe(true, true, false, true).IsProductionCertified);
        Assert.True(ServingReadiness.Describe(true, true, true, true).IsProductionCertified);
    }

    // ------------------------------------------------------ transport doubles

    private sealed class CompletingTransport : IModelTransport
    {
        private readonly ModelIdentity? _identity;
        private readonly List<string> _contacted = new();

        public CompletingTransport(ModelIdentity? identity) => _identity = identity;

        public string TransportId => "fake/completing";

        public ImmutableArray<string> ContactedEndpointIds => _contacted.ToImmutableArray();

        public Task<TransportResponse> SendAsync(TransportRequest request, CancellationToken cancellationToken)
        {
            _contacted.Add(request.EndpointId);
            return Task.FromResult(new TransportResponse(true, "a grounded answer", _identity, "ok"));
        }
    }

    private sealed class SlowTransport : IModelTransport
    {
        private readonly TimeSpan _delay;

        public SlowTransport(TimeSpan delay) => _delay = delay;

        public string TransportId => "fake/slow";

        public async Task<TransportResponse> SendAsync(TransportRequest request, CancellationToken cancellationToken)
        {
            await Task.Delay(_delay, cancellationToken);
            return new TransportResponse(true, "too late", null, "ok");
        }
    }

    private sealed class ThrowingTransport : IModelTransport
    {
        public string TransportId => "fake/throwing";

        public Task<TransportResponse> SendAsync(TransportRequest request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("the socket closed");
    }

    private sealed class FailingThenCompletingTransport : IModelTransport
    {
        private readonly ModelIdentity _identity;
        private readonly List<string> _contacted = new();

        public FailingThenCompletingTransport(ModelIdentity identity) => _identity = identity;

        public string TransportId => "fake/failing-then-completing";

        public ImmutableArray<string> ContactedEndpointIds => _contacted.ToImmutableArray();

        public Task<TransportResponse> SendAsync(TransportRequest request, CancellationToken cancellationToken)
        {
            _contacted.Add(request.EndpointId);

            return Task.FromResult(
                string.Equals(request.EndpointId, PrimaryEndpoint, StringComparison.Ordinal)
                    ? new TransportResponse(false, null, null, "the primary is unavailable")
                    : new TransportResponse(true, "a grounded answer", _identity, "ok"));
        }
    }

    /// <summary>An HTTP-shaped stub that records what it was handed, and asserts nothing itself.</summary>
    private sealed class HttpTranslatingStub : IModelTransport
    {
        private readonly ModelIdentity _identity;

        public HttpTranslatingStub(ModelIdentity identity) => _identity = identity;

        public string TransportId => "stub/http";

        public string? LastBody { get; private set; }

        public string? LastLocator { get; private set; }

        public TransportStyle LastStyle { get; private set; }

        public Task<TransportResponse> SendAsync(TransportRequest request, CancellationToken cancellationToken)
        {
            LastBody = request.Body;
            LastLocator = request.Locator;
            LastStyle = request.Style;

            return Task.FromResult(new TransportResponse(true, "a grounded answer", _identity, "200"));
        }
    }
}
