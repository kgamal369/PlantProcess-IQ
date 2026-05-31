// P00A-TEST-REGISTER: DELETE-ARCHIVED
// ArchivedAtUtc: 2026-05-31T11:07:14.744Z
// OriginalPath: Backend/tests/PlantProcess.PerformanceTests/PerformanceTestEnvironmentTests.cs
// Reason: No real performance assertion; replaced later by actual performance tests.

﻿using FluentAssertions;

namespace PlantProcess.PerformanceTests;

public sealed class PerformanceTestEnvironmentTests
{
    [Fact]
    public void Performance_test_project_should_run()
    {
        true.Should().BeTrue();
    }
}
