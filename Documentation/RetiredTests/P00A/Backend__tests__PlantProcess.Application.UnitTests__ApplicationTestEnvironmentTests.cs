// P00A-TEST-REGISTER: DELETE-ARCHIVED
// ArchivedAtUtc: 2026-05-31T11:07:14.744Z
// OriginalPath: Backend/tests/PlantProcess.Application.UnitTests/ApplicationTestEnvironmentTests.cs
// Reason: Assembly-load smoke only; superseded by real application unit tests.

﻿using FluentAssertions;
using PlantProcess.Application;

namespace PlantProcess.Application.UnitTests;

public sealed class ApplicationTestEnvironmentTests
{
    [Fact]
    public void Application_test_project_should_be_available()
    {
        typeof(ApplicationAssemblyMarker)
            .Assembly
            .GetName()
            .Name
            .Should()
            .Be("PlantProcess.Application");
    }
}
