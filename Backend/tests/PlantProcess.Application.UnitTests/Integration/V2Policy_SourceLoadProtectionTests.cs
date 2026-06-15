using System;
using System.Collections.Generic;
using PlantProcess.Application.Integration.Protection;
using Xunit;

namespace PlantProcess.Application.UnitTests.Integration;

public sealed class V2Policy_SourceLoadProtectionTests
{
    private static SourceLoadBudget Budget(TimeOnly? ws = null, TimeOnly? we = null) =>
        new(MaxRows: 1000, StatementTimeoutSeconds: 30, MaxQueriesPerMinute: 60, WindowStartUtc: ws, WindowEndUtc: we);

    [Fact]
    public void PPIQ_902_unbounded_query_is_rejected()
    {
        var d = SourceLoadProtectionPolicy.Evaluate(new(false, 0, 0, new TimeOnly(12, 0)), Budget());
        Assert.False(d.Allowed);
        Assert.Equal(SourceLoadRejectionReason.NoRowLimit, d.Reason);
    }

    [Fact]
    public void PPIQ_902_over_cap_query_is_rejected_with_typed_reason()
    {
        var d = SourceLoadProtectionPolicy.Evaluate(new(true, 5000, 0, new TimeOnly(12, 0)), Budget());
        Assert.False(d.Allowed);
        Assert.Equal(SourceLoadRejectionReason.RowCapExceeded, d.Reason);
        Assert.Equal(1000, d.EffectiveRowLimit);
    }

    [Fact]
    public void PPIQ_902_rate_limit_blocks()
    {
        var d = SourceLoadProtectionPolicy.Evaluate(new(true, 500, 60, new TimeOnly(12, 0)), Budget());
        Assert.Equal(SourceLoadRejectionReason.RateLimitExceeded, d.Reason);
    }

    [Fact]
    public void PPIQ_902_outside_overnight_window_is_blocked_inside_is_allowed()
    {
        var night = Budget(new TimeOnly(22, 0), new TimeOnly(6, 0));
        var blocked = SourceLoadProtectionPolicy.Evaluate(new(true, 500, 0, new TimeOnly(12, 0)), night);
        Assert.Equal(SourceLoadRejectionReason.OutsideApprovedWindow, blocked.Reason);
        var allowed = SourceLoadProtectionPolicy.Evaluate(new(true, 500, 0, new TimeOnly(3, 0)), night);
        Assert.True(allowed.Allowed);
    }

    [Fact]
    public void PPIQ_902_in_budget_query_is_allowed_with_statement_timeout()
    {
        var d = SourceLoadProtectionPolicy.Evaluate(new(true, 500, 0, new TimeOnly(2, 0)), Budget());
        Assert.True(d.Allowed);
        Assert.Equal(30, d.StatementTimeoutSeconds);
    }
}