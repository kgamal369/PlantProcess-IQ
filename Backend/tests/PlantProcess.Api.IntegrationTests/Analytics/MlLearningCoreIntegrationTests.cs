using Xunit;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using PlantProcess.Api.IntegrationTests.Infrastructure;

namespace PlantProcess.Api.IntegrationTests.Analytics;

public sealed class MlLearningCoreIntegrationTests : AuthenticatedApiTestBase
{
    public MlLearningCoreIntegrationTests(WebApplicationFactory<Program> factory)
        : base(factory)
    {
    }

    [SkippableFact]
    public async Task Ml_learning_core_should_expose_status_jobs_run_results_and_provider_proof()
    {
        using var client = await CreateAuthenticatedClientAsync();

        var statusResponse = await client.GetAsync("/api/ml/learning/status");
        statusResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var jobsResponse = await client.GetAsync("/api/ml/learning/jobs");
        jobsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var jobsJson = await jobsResponse.Content.ReadFromJsonAsync<JsonElement>();
        var jobs = jobsJson.GetProperty("jobs").EnumerateArray().ToList();

        jobs.Should().HaveCountGreaterThanOrEqualTo(4);
        jobs.Select(x => x.GetProperty("job_code").GetString())
            .Should()
            .Contain(new[]
            {
                "ML_PROCESS_VS_DEFECT",
                "ML_PROCESS_VS_DOWNTIME",
                "ML_PROCESS_VS_KPI",
                "ML_WEEKLY_OVERALL"
            });

        var runResponse = await client.PostAsJsonAsync(
            "/api/ml/learning/jobs/ML_PROCESS_VS_DEFECT/run",
            new
            {
                outcomeFamily = "defect",
                windowDays = 730
            });

        runResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var runJson = await runResponse.Content.ReadFromJsonAsync<JsonElement>();
        runJson.GetProperty("result").GetProperty("status").GetString()
            .Should()
            .Be("Completed");

        var resultsResponse = await client.GetAsync("/api/ml/learning/results?limit=200&jobCode=ML_PROCESS_VS_DEFECT");
        resultsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var resultsJson = await resultsResponse.Content.ReadFromJsonAsync<JsonElement>();
        var results = resultsJson.GetProperty("results").EnumerateArray().ToList();

        results.Should().NotBeEmpty("the ML learning job must persist result rows");

        results.Should().Contain(row =>
            GetString(row, "finding_status") == "EvidenceForReview" &&
            GetNullableDecimal(row, "q_value").HasValue &&
            GetNullableDecimal(row, "effect_size") >= 0.5m);

        var statusText = await statusResponse.Content.ReadAsStringAsync();

        statusText.Should().Contain(
            "rejectsNoiseControl",
            "the acceptance endpoint must prove the golden noise-control rejection even when the paged /results list is ordered by surfaced signals");

        statusText.Should().Contain(
            "true",
            "the acceptance endpoint must report rejectsNoiseControl=true");

        var proofResponse = await client.GetAsync("/api/ml/providers/narrative/proof");
        proofResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var proofJson = await proofResponse.Content.ReadFromJsonAsync<JsonElement>();

        proofJson.GetProperty("usedExternalApi").GetBoolean()
            .Should()
            .BeFalse("local/offline narrative provider must degrade safely without external API");

        proofJson.GetProperty("rawPlantDataIncluded").GetBoolean()
            .Should()
            .BeFalse("narrative provider must not leak raw plant data");
    }

    [SkippableFact]
    public async Task Ml_results_endpoint_should_keep_honest_positioning_language()
    {
        using var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/api/ml/learning/results?limit=20");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var honestPositioning = json.GetProperty("honestPositioning").GetString();

        honestPositioning.Should().NotBeNullOrWhiteSpace();
        honestPositioning!.Should().Contain("Diagnostic associations");
        honestPositioning.Should().Contain("not guaranteed root cause");
    }

    private static string? GetString(JsonElement row, string propertyName)
    {
        return row.TryGetProperty(propertyName, out var property)
            ? property.GetString()
            : null;
    }

    private static decimal? GetNullableDecimal(JsonElement row, string propertyName)
    {
        if (!row.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        if (property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        return property.TryGetDecimal(out var value)
            ? value
            : null;
    }
}
