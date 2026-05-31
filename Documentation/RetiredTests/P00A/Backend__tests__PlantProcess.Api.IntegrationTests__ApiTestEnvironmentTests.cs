// P00A-TEST-REGISTER: DELETE-ARCHIVED
// ArchivedAtUtc: 2026-05-31T11:07:14.744Z
// OriginalPath: Backend/tests/PlantProcess.Api.IntegrationTests/ApiTestEnvironmentTests.cs
// Reason: No-op integration smoke test; superseded by real API integration tests.

﻿using FluentAssertions;

namespace PlantProcess.Api.IntegrationTests;

public sealed class ApiTestEnvironmentTests
{
    [Fact]
    public void Api_integration_test_project_should_run()
    {
        true.Should().BeTrue();
    }
}
