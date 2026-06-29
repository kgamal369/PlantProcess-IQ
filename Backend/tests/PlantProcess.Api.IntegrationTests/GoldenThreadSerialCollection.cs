using Xunit;
using Microsoft.AspNetCore.Mvc.Testing;
using PlantProcess.Api.IntegrationTests.Infrastructure;

namespace PlantProcess.Api.IntegrationTests;

// Golden-thread tests share the C-0044170 genealogy edges. One sibling class re-runs
// seed 010 (DELETE+INSERT of those edges) at test time; the readers must not run in
// parallel with that reseed or they observe the coil mid-rewrite (0.50 instead of 0.70/0.30).
// This collection forces them to run serially. Tests share the WebApplicationFactory fixture.
[CollectionDefinition("GoldenThreadSerial", DisableParallelization = true)]
public sealed class GoldenThreadSerialCollection : ICollectionFixture<WebApplicationFactory<Program>>
{
}
