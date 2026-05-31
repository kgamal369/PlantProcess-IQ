// P00A-TEST-REGISTER: DELETE-ARCHIVED
// ArchivedAtUtc: 2026-05-31T11:07:14.744Z
// OriginalPath: Backend/tests/PlantProcess.Domain.Tests/DomainTestEnvironmentTests.cs
// Reason: Assembly-load smoke only; superseded by real domain tests.

﻿using FluentAssertions;

namespace PlantProcess.Domain.Tests;

public sealed class DomainTestEnvironmentTests
{
    [Fact]
    public void Domain_test_project_should_be_available()
    {
        typeof(PlantProcess.Domain.Common.BaseEntity)
            .Assembly
            .GetName()
            .Name
            .Should()
            .Be("PlantProcess.Domain");
    }
}
