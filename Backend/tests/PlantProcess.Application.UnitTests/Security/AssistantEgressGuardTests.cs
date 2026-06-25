using PlantProcess.Application.Security.DataBoundary;
using Xunit;

namespace PlantProcess.Application.UnitTests.Security;

public sealed class AssistantEgressGuardTests
{
    private static readonly string[] Handles = { "evidence-1", "evidence-2" };
    private static readonly string[] Chunks = { "coil 4471 width 1250mm", "defect edge-crack" };

    [Fact]
    public void Self_hosted_no_egress_with_external_model_never_egresses_and_answers_locally()
    {
        var plan = AssistantEgressGuard.Plan(true, true, "why did yield drop", Handles, Chunks);
        Assert.Equal(AssistantBoundaryOutcome.BlockedNoEgress, plan.Outcome);
        Assert.False(plan.RemoteEgressAllowed);
        Assert.True(plan.UseLocalProvider);
    }

    [Fact]
    public void Permitted_remote_endpoint_sends_only_question_and_handles()
    {
        var plan = AssistantEgressGuard.Plan(false, true, "why did yield drop", Handles, Chunks);
        Assert.Equal(AssistantBoundaryOutcome.RemoteAllowedScopedOnly, plan.Outcome);
        Assert.True(plan.RemoteEgressAllowed);
        Assert.False(plan.UseLocalProvider);
        Assert.Empty(plan.EgressChunks);
    }

    [Fact]
    public void Local_model_keeps_full_context_in_tenant()
    {
        var plan = AssistantEgressGuard.Plan(true, false, "why did yield drop", Handles, Chunks);
        Assert.Equal(AssistantBoundaryOutcome.LocalAllowed, plan.Outcome);
        Assert.False(plan.RemoteEgressAllowed);
        Assert.True(plan.UseLocalProvider);
        Assert.Equal(Chunks.Length, plan.EgressChunks.Count);
    }

    [Fact]
    public void Toggle_flips_same_external_model_between_blocked_and_scoped_remote()
    {
        var on = AssistantEgressGuard.Plan(true, true, "q", Handles, Chunks);
        var off = AssistantEgressGuard.Plan(false, true, "q", Handles, Chunks);
        Assert.Equal(AssistantBoundaryOutcome.BlockedNoEgress, on.Outcome);
        Assert.Equal(AssistantBoundaryOutcome.RemoteAllowedScopedOnly, off.Outcome);
    }
}