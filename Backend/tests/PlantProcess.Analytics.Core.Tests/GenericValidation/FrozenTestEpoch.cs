// Frozen epoch for the generic validation fixture.
//
// Backlog origin: T-208.
//
// Nothing in the fixture reads the wall clock. Every instant is an offset from this
// constant, so two runs a month apart produce byte-identical inputs.
using System;

namespace PlantProcess.Analytics.Core.Tests.GenericValidation;

public static class FrozenTestEpoch
{
    public static readonly DateTimeOffset Origin = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static DateTimeOffset AtMinute(double minutes) => Origin.AddMinutes(minutes);
}