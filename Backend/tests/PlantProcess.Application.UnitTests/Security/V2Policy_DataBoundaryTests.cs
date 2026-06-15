using System.Collections.Generic;
using PlantProcess.Application.Security.DataBoundary;
using Xunit;

namespace PlantProcess.Application.UnitTests.Security;

public sealed class V2Policy_DataBoundaryTests
{
    private static readonly IReadOnlyList<string> Handles = new[] { "evi:coil:42", "evi:heat:7" };

    [Fact]
    public void PPIQ_904_payload_with_plant_data_is_blocked()
    {
        var d = DataBoundaryPolicy.Evaluate(new(false, false, "why defect?", Handles, PayloadContainsPlantData: true));
        Assert.False(d.Allowed);
        Assert.Equal(AssistantBoundaryOutcome.BlockedPlantDataLeak, d.Outcome);
    }

    [Fact]
    public void PPIQ_904_no_egress_with_remote_model_disables_the_path()
    {
        var d = DataBoundaryPolicy.Evaluate(new(NoEgressEnabled: true, ModelIsLocal: false, "q", Handles, false));
        Assert.False(d.Allowed);
        Assert.Equal(AssistantBoundaryOutcome.BlockedNoEgress, d.Outcome);
    }

    [Fact]
    public void PPIQ_904_no_egress_with_local_model_is_allowed_locally()
    {
        var d = DataBoundaryPolicy.Evaluate(new(NoEgressEnabled: true, ModelIsLocal: true, "q", Handles, false));
        Assert.True(d.Allowed);
        Assert.Equal(AssistantBoundaryOutcome.LocalAllowed, d.Outcome);
    }

    [Fact]
    public void PPIQ_904_remote_allowed_carries_only_question_and_scoped_handles()
    {
        var d = DataBoundaryPolicy.Evaluate(new(false, false, "q", Handles, PayloadContainsPlantData: false));
        Assert.True(d.Allowed);
        Assert.Equal(AssistantBoundaryOutcome.RemoteAllowedScopedOnly, d.Outcome);
    }
}