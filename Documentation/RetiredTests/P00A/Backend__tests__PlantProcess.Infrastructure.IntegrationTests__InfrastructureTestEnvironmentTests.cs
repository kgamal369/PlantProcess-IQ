// P00A-TEST-REGISTER: DELETE-ARCHIVED
// ArchivedAtUtc: 2026-05-31T11:07:14.744Z
// OriginalPath: Backend/tests/PlantProcess.Infrastructure.IntegrationTests/InfrastructureTestEnvironmentTests.cs
// Reason: Assembly-load smoke only; superseded by real infrastructure integration tests.

﻿using FluentAssertions;
using PlantProcess.Infrastructure.Persistence;

namespace PlantProcess.Infrastructure.IntegrationTests;

public sealed class InfrastructureTestEnvironmentTests
{
    [Fact]
    public void Infrastructure_test_project_should_reference_db_context()
    {
        typeof(PlantProcessDbContext)
            .Assembly
            .GetName()
            .Name
            .Should()
            .Be("PlantProcess.Infrastructure");
    }
}
