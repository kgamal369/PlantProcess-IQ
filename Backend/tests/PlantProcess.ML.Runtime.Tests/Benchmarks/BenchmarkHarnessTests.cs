// T-182 - Falsification of the common B-01..B-09 benchmark harness.
//
// Each test states an observable property of the machinery. None of them asserts
// a benchmark value, because this task measures nothing about the product.

using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using PlantProcess.ML.Runtime.Benchmarks;

namespace PlantProcess.ML.Runtime.Tests.Benchmarks;

public sealed class BenchmarkHarnessTests
{
    private const string TestCommit = "0000000000000000000000000000000000000000";
    private const string TestCapturedAt = "2026-01-01T00:00:00Z";

    private static BenchmarkRunner Runner()
    {
        return new BenchmarkRunner(FixedEnvironmentProbe.Deterministic());
    }

    private static BenchmarkSuiteResult Suite()
    {
        return BenchmarkSuite.RunAll(FixedEnvironmentProbe.Deterministic(), TestCommit, TestCapturedAt);
    }

    // --- registration and definitions --------------------------------------

    [Fact]
    public void All_nine_benchmarks_are_registered()
    {
        IReadOnlyList<string> registered = BenchmarkSuite.RegisteredBenchmarkIds();

        Assert.Equal(9, registered.Count);
        Assert.Equal(
            new[] { "B-01", "B-02", "B-03", "B-04", "B-05", "B-06", "B-07", "B-08", "B-09" },
            registered.ToArray());
    }

    [Fact]
    public void Every_benchmark_carries_a_real_frozen_definition()
    {
        foreach (BenchmarkDefinition definition in BenchmarkCatalogue.All())
        {
            Assert.True(BenchmarkCatalogue.IsKnownBenchmarkId(definition.BenchmarkId));
            Assert.False(string.IsNullOrWhiteSpace(definition.Title));
            Assert.False(string.IsNullOrWhiteSpace(definition.Question));
            Assert.False(string.IsNullOrWhiteSpace(definition.Method));
            Assert.False(string.IsNullOrWhiteSpace(definition.Decides));
            Assert.False(string.IsNullOrWhiteSpace(definition.DecisionOwner));
            Assert.False(string.IsNullOrWhiteSpace(definition.SubjectKind));
            Assert.Equal(BenchmarkCatalogue.FrozenRegister, definition.DefinitionSource);
            Assert.NotEmpty(definition.ApplicableFamilies);
        }
    }

    [Fact]
    public void No_definition_is_a_placeholder()
    {
        string[] placeholderTokens =
        {
            "NotSupplied", "not supplied", "TBD", "placeholder", "unknown", "TODO"
        };

        foreach (BenchmarkDefinition definition in BenchmarkCatalogue.All())
        {
            string text = definition.Title + " " + definition.Question + " "
                        + definition.Method + " " + definition.Decides;

            foreach (string token in placeholderTokens)
            {
                Assert.DoesNotContain(token, text, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public void Unknown_benchmark_id_is_rejected()
    {
        Assert.False(BenchmarkCatalogue.IsKnownBenchmarkId("B-10"));
        Assert.False(BenchmarkCatalogue.IsKnownBenchmarkId(null));
        Assert.Throws<ArgumentException>(() => BenchmarkCatalogue.Get("B-10"));
        Assert.Throws<ArgumentException>(() => BenchmarkFixtures.Synthetic("B-10"));
    }

    // --- synthetic smoke, one per B-ID --------------------------------------

    [Fact]
    public void Every_benchmark_executes_a_synthetic_smoke_and_reaches_measured()
    {
        BenchmarkSuiteResult result = Suite();

        Assert.Equal(9, result.SyntheticResults.Count);

        foreach (string benchmarkId in BenchmarkCatalogue.AllBenchmarkIds)
        {
            BenchmarkEnvelope envelope = result.SyntheticResults
                .Single(e => e.BenchmarkId == benchmarkId);

            Assert.Equal(BenchmarkTerminalState.Measured, envelope.TerminalState);
            Assert.True(envelope.Measurements.Count > 0);
            Assert.Null(envelope.Validate());
            Assert.Equal(MeasurementScope.Synthetic, envelope.Scope);
            Assert.True(envelope.SyntheticSmokeMeasured);
            Assert.False(envelope.ProductionMeasured);
        }
    }

    // One named test result per B-ID, so the smoke table in the pack report is read
    // from the TRX rather than narrated.

    [Theory]
    [InlineData("B-01")]
    [InlineData("B-02")]
    [InlineData("B-03")]
    [InlineData("B-04")]
    [InlineData("B-05")]
    [InlineData("B-06")]
    [InlineData("B-07")]
    [InlineData("B-08")]
    [InlineData("B-09")]
    public void Synthetic_smoke_measures(string benchmarkId)
    {
        BenchmarkEnvelope envelope = Runner().Run(
            BenchmarkFixtures.Synthetic(benchmarkId),
            BenchmarkSuite.RequestFor(benchmarkId, TestCommit, TestCapturedAt));

        Assert.Null(envelope.Validate());
        Assert.Equal(BenchmarkTerminalState.Measured, envelope.TerminalState);
        Assert.NotEmpty(envelope.Measurements);
        Assert.True(envelope.HarnessImplemented);
        Assert.True(envelope.SyntheticSmokeMeasured);
        Assert.False(envelope.ProductionMeasured);
        Assert.False(envelope.SelectedValue);
    }

    [Theory]
    [InlineData("B-01")]
    [InlineData("B-02")]
    [InlineData("B-03")]
    [InlineData("B-04")]
    [InlineData("B-05")]
    [InlineData("B-06")]
    [InlineData("B-07")]
    [InlineData("B-08")]
    [InlineData("B-09")]
    public void Production_measurement_is_unavailable_and_owned(string benchmarkId)
    {
        BenchmarkEnvelope envelope = Runner().Run(
            BenchmarkFixtures.Production(benchmarkId),
            BenchmarkSuite.RequestFor(benchmarkId, TestCommit, TestCapturedAt));

        Assert.Null(envelope.Validate());
        Assert.Equal(BenchmarkTerminalState.CapabilityUnavailable, envelope.TerminalState);
        Assert.Empty(envelope.Measurements);
        Assert.False(envelope.ProductionMeasured);
        Assert.False(string.IsNullOrWhiteSpace(envelope.OwnerTrigger));
    }

    [Fact]
    public void Every_subject_declares_the_domain_kind_its_definition_names()
    {
        foreach (string benchmarkId in BenchmarkCatalogue.AllBenchmarkIds)
        {
            string expected = BenchmarkCatalogue.Get(benchmarkId).SubjectKind;

            Assert.Equal(expected, BenchmarkFixtures.Synthetic(benchmarkId).Identity.Kind);
            Assert.Equal(expected, BenchmarkFixtures.Production(benchmarkId).Identity.Kind);
        }
    }

    [Fact]
    public void Every_metric_belongs_to_a_family_its_definition_admits()
    {
        BenchmarkSuiteResult result = Suite();

        foreach (BenchmarkEnvelope envelope in result.SyntheticResults)
        {
            BenchmarkDefinition definition = BenchmarkCatalogue.Get(envelope.BenchmarkId);
            foreach (MetricValue metric in envelope.Measurements)
            {
                Assert.True(definition.Admits(metric.Family));
            }
        }
    }

    // --- the two B-09 records are not collapsed -----------------------------

    [Fact]
    public void The_serving_stub_and_the_real_serving_runtime_are_two_separate_records()
    {
        BenchmarkEnvelope stub = Runner().Run(
            BenchmarkFixtures.Synthetic("B-09"),
            BenchmarkSuite.RequestFor("B-09", TestCommit, TestCapturedAt));

        BenchmarkEnvelope real = Runner().Run(
            BenchmarkFixtures.Production("B-09"),
            BenchmarkSuite.RequestFor("B-09", TestCommit, TestCapturedAt));

        Assert.Equal("ModelServingRuntime", stub.Subject.Kind);
        Assert.Equal(SubjectFixtureKind.DeterministicStub, stub.Subject.FixtureKind);
        Assert.Equal(BenchmarkTerminalState.Measured, stub.TerminalState);
        Assert.True(stub.SyntheticSmokeMeasured);
        Assert.False(stub.ProductionMeasured);
        Assert.False(stub.SelectedValue);

        Assert.Equal("ModelServingRuntime", real.Subject.Kind);
        Assert.Equal(SubjectFixtureKind.RealSubject, real.Subject.FixtureKind);
        Assert.Equal(BenchmarkTerminalState.CapabilityUnavailable, real.TerminalState);
        Assert.False(real.SyntheticSmokeMeasured);
        Assert.False(real.ProductionMeasured);
        Assert.Contains("T-138", real.OwnerTrigger!);

        Assert.NotEqual(stub.ExperimentId, real.ExperimentId);
    }

    [Fact]
    public void A_deterministic_fixture_cannot_declare_itself_a_real_subject()
    {
        Assert.Throws<ArgumentException>(() => new DeterministicFixtureSubject(
            "B-01",
            "liar",
            SubjectFixtureKind.RealSubject,
            Array.Empty<FixtureMetricSpec>(),
            Array.Empty<FixtureMetricSpec>()));
    }

    [Fact]
    public void The_four_readiness_facts_are_independent()
    {
        BenchmarkSuiteResult result = Suite();

        Assert.Equal(9, result.Readiness.Count);

        foreach (BenchmarkReadiness readiness in result.Readiness)
        {
            Assert.True(readiness.HarnessImplemented);
            Assert.True(readiness.SyntheticSmokeMeasured);
            Assert.False(readiness.ProductionMeasured);
            Assert.False(readiness.SelectedValue);
            Assert.False(string.IsNullOrWhiteSpace(readiness.OwnerTrigger));
        }

        Assert.Equal(9, result.NotMeasuredBenchmarkIds().Count);
    }

    // --- absent subject -----------------------------------------------------

    [Fact]
    public void Every_production_subject_is_capability_unavailable_with_reason_and_owner()
    {
        foreach (string benchmarkId in BenchmarkCatalogue.AllBenchmarkIds)
        {
            BenchmarkEnvelope envelope = Runner().Run(
                BenchmarkFixtures.Production(benchmarkId),
                BenchmarkSuite.RequestFor(benchmarkId, TestCommit, TestCapturedAt));

            Assert.Equal(BenchmarkTerminalState.CapabilityUnavailable, envelope.TerminalState);
            Assert.False(string.IsNullOrWhiteSpace(envelope.RefusalReason));
            Assert.False(string.IsNullOrWhiteSpace(envelope.OwnerTrigger));
            Assert.Null(envelope.Validate());
        }
    }

    [Fact]
    public void Missing_metric_is_absent_and_never_zero()
    {
        BenchmarkEnvelope envelope = Runner().Run(
            BenchmarkFixtures.Production("B-09"),
            BenchmarkSuite.RequestFor("B-09", TestCommit, TestCapturedAt));

        Assert.Empty(envelope.Measurements);
        Assert.False(envelope.TryGetMetric(
            "time_to_first_token", MetricAggregation.P95, out MetricValue _));
    }

    // --- envelope invariants ------------------------------------------------

    [Fact]
    public void Measured_state_without_measurements_is_invalid()
    {
        BenchmarkEnvelope envelope = MeasuredEnvelope() with
        {
            Measurements = Array.Empty<MetricValue>()
        };

        Assert.NotNull(envelope.Validate());
    }

    [Fact]
    public void Refusal_state_carrying_measurements_is_invalid()
    {
        BenchmarkEnvelope envelope = MeasuredEnvelope() with
        {
            TerminalState = BenchmarkTerminalState.NotMeasured,
            RefusalReason = "reason",
            OwnerTrigger = "owner"
        };

        Assert.NotNull(envelope.Validate());
    }

    [Fact]
    public void Refusal_state_without_a_reason_is_invalid()
    {
        BenchmarkEnvelope envelope = MeasuredEnvelope() with
        {
            TerminalState = BenchmarkTerminalState.NotMeasured,
            Measurements = Array.Empty<MetricValue>(),
            RefusalReason = null,
            OwnerTrigger = null
        };

        Assert.NotNull(envelope.Validate());
    }

    [Fact]
    public void Empty_dataset_hash_fails_closed()
    {
        BenchmarkRequest request = BenchmarkSuite.RequestFor("B-01", TestCommit, TestCapturedAt);
        request = request with { Workload = request.Workload with { DatasetHash = string.Empty } };

        BenchmarkEnvelope envelope = Runner().Run(BenchmarkFixtures.Synthetic("B-01"), request);

        Assert.Equal(BenchmarkTerminalState.InvalidInput, envelope.TerminalState);
    }

    [Fact]
    public void Empty_snapshot_hash_fails_closed()
    {
        BenchmarkRequest request = BenchmarkSuite.RequestFor("B-03", TestCommit, TestCapturedAt);
        request = request with { Workload = request.Workload with { SnapshotHash = string.Empty } };

        BenchmarkEnvelope envelope = Runner().Run(BenchmarkFixtures.Synthetic("B-03"), request);

        Assert.Equal(BenchmarkTerminalState.InvalidInput, envelope.TerminalState);
    }

    [Fact]
    public void Zero_repetitions_fails_closed()
    {
        BenchmarkRequest request = BenchmarkSuite.RequestFor("B-01", TestCommit, TestCapturedAt);
        request = request with { Execution = ExecutionPolicy.Of(0, 0) };

        BenchmarkEnvelope envelope = Runner().Run(BenchmarkFixtures.Synthetic("B-01"), request);

        Assert.Equal(BenchmarkTerminalState.InvalidInput, envelope.TerminalState);
    }

    [Fact]
    public void Subject_registered_for_another_benchmark_fails_closed()
    {
        BenchmarkEnvelope envelope = Runner().Run(
            BenchmarkFixtures.Synthetic("B-06"),
            BenchmarkSuite.RequestFor("B-01", TestCommit, TestCapturedAt));

        Assert.Equal(BenchmarkTerminalState.InvalidInput, envelope.TerminalState);
    }

    // --- metric contract ----------------------------------------------------

    [Fact]
    public void Illegal_unit_for_a_family_fails_closed()
    {
        BenchmarkEnvelope envelope = Runner().Run(
            new HostileSubject(
                "B-01",
                MetricValue.Create("loader_throughput", MetricFamily.Throughput, 1.0,
                    MetricContract.MebibytesUnit, MetricAggregation.Sample)),
            BenchmarkSuite.RequestFor("B-01", TestCommit, TestCapturedAt));

        Assert.Equal(BenchmarkTerminalState.InvalidInput, envelope.TerminalState);
        Assert.Contains("not legal for family", envelope.RefusalReason!);
    }

    [Fact]
    public void Illegal_aggregation_for_a_family_fails_closed()
    {
        BenchmarkEnvelope envelope = Runner().Run(
            new HostileSubject(
                "B-07",
                MetricValue.Create("packed_evidence_tokens", MetricFamily.Count, 10.0,
                    MetricContract.CountUnit, MetricAggregation.P95)),
            BenchmarkSuite.RequestFor("B-07", TestCommit, TestCapturedAt));

        Assert.Equal(BenchmarkTerminalState.InvalidInput, envelope.TerminalState);
    }

    [Fact]
    public void Non_finite_value_fails_closed()
    {
        BenchmarkEnvelope envelope = Runner().Run(
            new HostileSubject(
                "B-01",
                MetricValue.Create("random_access_cost", MetricFamily.Latency, double.NaN,
                    MetricContract.MillisecondsUnit, MetricAggregation.Sample)),
            BenchmarkSuite.RequestFor("B-01", TestCommit, TestCapturedAt));

        Assert.Equal(BenchmarkTerminalState.InvalidInput, envelope.TerminalState);
    }

    [Fact]
    public void A_ratio_outside_zero_to_one_fails_closed()
    {
        BenchmarkEnvelope envelope = Runner().Run(
            new HostileSubject(
                "B-07",
                MetricValue.Create("groundedness", MetricFamily.Quality, 1.4,
                    MetricContract.RatioUnit, MetricAggregation.Sample)),
            BenchmarkSuite.RequestFor("B-07", TestCommit, TestCapturedAt));

        Assert.Equal(BenchmarkTerminalState.InvalidInput, envelope.TerminalState);
    }

    [Fact]
    public void Metric_outside_the_benchmarks_admitted_families_is_refused()
    {
        // B-08 decides whether re-ranking earns its latency. VRAM is not part of
        // that question, so the harness refuses it rather than reporting it.
        BenchmarkEnvelope envelope = Runner().Run(
            new HostileSubject(
                "B-08",
                MetricValue.Create("vram_per_session", MetricFamily.Vram, 4096.0,
                    MetricContract.MebibytesUnit, MetricAggregation.Sample)),
            BenchmarkSuite.RequestFor("B-08", TestCommit, TestCapturedAt));

        Assert.Equal(BenchmarkTerminalState.InvalidInput, envelope.TerminalState);
        Assert.Contains("does not admit", envelope.RefusalReason!);
    }

    [Fact]
    public void Subject_that_produces_nothing_is_not_measured()
    {
        BenchmarkEnvelope envelope = Runner().Run(
            new SilentSubject("B-01"),
            BenchmarkSuite.RequestFor("B-01", TestCommit, TestCapturedAt));

        Assert.Equal(BenchmarkTerminalState.NotMeasured, envelope.TerminalState);
        Assert.Empty(envelope.Measurements);
    }

    [Fact]
    public void Subject_that_throws_is_failed_not_measured()
    {
        BenchmarkEnvelope envelope = Runner().Run(
            new ThrowingSubject("B-01"),
            BenchmarkSuite.RequestFor("B-01", TestCommit, TestCapturedAt));

        Assert.Equal(BenchmarkTerminalState.Failed, envelope.TerminalState);
        Assert.Empty(envelope.Measurements);
    }

    [Fact]
    public void Every_measurement_carries_an_explicit_and_legal_unit()
    {
        foreach (BenchmarkEnvelope envelope in Suite().SyntheticResults)
        {
            foreach (MetricValue metric in envelope.Measurements)
            {
                Assert.False(string.IsNullOrWhiteSpace(metric.Unit));
                Assert.True(MetricContract.IsAllowedUnit(metric.Family, metric.Unit));
                Assert.True(MetricContract.IsAllowedAggregation(metric.Family, metric.Aggregation));
            }
        }
    }

    // --- percentiles --------------------------------------------------------

    [Fact]
    public void Percentiles_are_nearest_rank_and_deterministic()
    {
        double[] samples = { 5.0, 1.0, 4.0, 2.0, 3.0 };

        Assert.Equal(3.0, Percentiles.P50(samples));
        Assert.Equal(5.0, Percentiles.P95(samples));
        Assert.Equal(5.0, Percentiles.P99(samples));
        Assert.Equal(3.0, Percentiles.Mean(samples));
    }

    [Fact]
    public void Percentile_of_an_empty_sample_set_throws_rather_than_returning_zero()
    {
        Assert.Throws<ArgumentException>(() => Percentiles.P95(Array.Empty<double>()));
    }

    [Fact]
    public void Percentiles_are_emitted_only_for_latency_metrics()
    {
        BenchmarkEnvelope envelope = Runner().Run(
            BenchmarkFixtures.Synthetic("B-01"),
            BenchmarkSuite.RequestFor("B-01", TestCommit, TestCapturedAt));

        Assert.True(envelope.TryGetMetric(
            "random_access_cost", MetricAggregation.P95, out MetricValue _));
        Assert.False(envelope.TryGetMetric(
            "loader_throughput", MetricAggregation.P95, out MetricValue _));
    }

    // --- experiment identity ------------------------------------------------

    [Fact]
    public void Same_fixture_preserves_experiment_identity_and_values()
    {
        BenchmarkEnvelope first = Runner().Run(
            BenchmarkFixtures.Synthetic("B-06"),
            BenchmarkSuite.RequestFor("B-06", TestCommit, TestCapturedAt));

        BenchmarkEnvelope second = Runner().Run(
            BenchmarkFixtures.Synthetic("B-06"),
            BenchmarkSuite.RequestFor("B-06", TestCommit, TestCapturedAt));

        Assert.Equal(first.ExperimentId, second.ExperimentId);
        Assert.Equal(
            first.Measurements.Select(m => m.Value).ToArray(),
            second.Measurements.Select(m => m.Value).ToArray());
    }

    [Fact]
    public void Experiment_identity_is_independent_of_commit_and_environment()
    {
        BenchmarkEnvelope first = Runner().Run(
            BenchmarkFixtures.Synthetic("B-06"),
            BenchmarkSuite.RequestFor("B-06", TestCommit, TestCapturedAt));

        BenchmarkRequest other = BenchmarkSuite.RequestFor(
            "B-06", "ffffffffffffffffffffffffffffffffffffffff", "2030-01-01T00:00:00Z");

        BenchmarkEnvelope second = new BenchmarkRunner(new HostEnvironmentProbe())
            .Run(BenchmarkFixtures.Synthetic("B-06"), other);

        Assert.Equal(first.ExperimentId, second.ExperimentId);
        Assert.NotEqual(first.Reproducibility.GitCommit, second.Reproducibility.GitCommit);
    }

    [Fact]
    public void Different_parameters_produce_a_different_experiment_identity()
    {
        BenchmarkRequest baseline = BenchmarkSuite.RequestFor("B-06", TestCommit, TestCapturedAt);
        BenchmarkRequest varied = baseline with
        {
            Workload = baseline.Workload with
            {
                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["profile"] = "smoke",
                    ["scale"] = "8"
                }
            }
        };

        Assert.NotEqual(
            Runner().Run(BenchmarkFixtures.Synthetic("B-06"), baseline).ExperimentId,
            Runner().Run(BenchmarkFixtures.Synthetic("B-06"), varied).ExperimentId);
    }

    [Fact]
    public void A_different_snapshot_hash_produces_a_different_experiment_identity()
    {
        BenchmarkRequest baseline = BenchmarkSuite.RequestFor("B-03", TestCommit, TestCapturedAt);
        BenchmarkRequest varied = baseline with
        {
            Workload = baseline.Workload with { SnapshotHash = "sha256:another-snapshot" }
        };

        Assert.NotEqual(
            Runner().Run(BenchmarkFixtures.Synthetic("B-03"), baseline).ExperimentId,
            Runner().Run(BenchmarkFixtures.Synthetic("B-03"), varied).ExperimentId);
    }

    [Fact]
    public void Commit_environment_and_workload_identity_are_recorded()
    {
        BenchmarkEnvelope envelope = Runner().Run(
            BenchmarkFixtures.Synthetic("B-05"),
            BenchmarkSuite.RequestFor("B-05", TestCommit, TestCapturedAt));

        Assert.Equal(TestCommit, envelope.Reproducibility.GitCommit);
        Assert.Equal(BenchmarkRunner.HarnessVersion, envelope.Reproducibility.HarnessVersion);
        Assert.False(string.IsNullOrWhiteSpace(envelope.Environment.OperatingSystem));
        Assert.False(string.IsNullOrWhiteSpace(envelope.Environment.RuntimeVersion));
        Assert.False(string.IsNullOrWhiteSpace(envelope.Workload.DatasetHash));
        Assert.False(string.IsNullOrWhiteSpace(envelope.Workload.SnapshotHash));
        Assert.Equal(20, envelope.SampleCount);
    }

    // --- selected values ----------------------------------------------------

    [Fact]
    public void A_synthetic_result_cannot_become_a_selected_value()
    {
        foreach (string benchmarkId in BenchmarkCatalogue.AllBenchmarkIds)
        {
            BenchmarkEnvelope envelope = Runner().Run(
                BenchmarkFixtures.Synthetic(benchmarkId),
                BenchmarkSuite.RequestFor(benchmarkId, TestCommit, TestCapturedAt));

            MetricValue any = envelope.Measurements[0];

            bool published = SelectedValueGuard.TryPublish(
                envelope, any.Name, any.Aggregation,
                out SelectedBenchmarkValue? selected, out string refusal);

            Assert.False(published);
            Assert.Null(selected);
            Assert.Contains("fixture", refusal);
        }
    }

    [Fact]
    public void An_unavailable_result_cannot_become_a_selected_value()
    {
        BenchmarkEnvelope envelope = Runner().Run(
            BenchmarkFixtures.Production("B-09"),
            BenchmarkSuite.RequestFor("B-09", TestCommit, TestCapturedAt));

        bool published = SelectedValueGuard.TryPublish(
            envelope, "time_to_first_token", MetricAggregation.P95,
            out SelectedBenchmarkValue? selected, out string refusal);

        Assert.False(published);
        Assert.Null(selected);
        Assert.False(string.IsNullOrWhiteSpace(refusal));
    }

    [Fact]
    public void A_selected_value_requires_the_named_metric_to_be_present()
    {
        BenchmarkEnvelope envelope = Runner().Run(
            new RealSubjectForGuardTest("B-09"),
            BenchmarkSuite.RequestFor("B-09", TestCommit, TestCapturedAt));

        bool published = SelectedValueGuard.TryPublish(
            envelope, "a_metric_nobody_measured", MetricAggregation.P95,
            out SelectedBenchmarkValue? selected, out string refusal);

        Assert.False(published);
        Assert.Null(selected);
        Assert.Contains("is not zero", refusal);
    }

    [Fact]
    public void A_real_production_measurement_can_become_a_selected_value()
    {
        BenchmarkEnvelope envelope = Runner().Run(
            new RealSubjectForGuardTest("B-09"),
            BenchmarkSuite.RequestFor("B-09", TestCommit, TestCapturedAt));

        Assert.True(envelope.IsProductionEvidence());
        Assert.True(envelope.ProductionMeasured);
        Assert.False(envelope.SyntheticSmokeMeasured);

        bool published = SelectedValueGuard.TryPublish(
            envelope, "time_to_first_token", MetricAggregation.P95,
            out SelectedBenchmarkValue? selected, out string refusal);

        Assert.True(published, refusal);
        Assert.NotNull(selected);
        Assert.Equal("B-09", selected!.BenchmarkId);
        Assert.Equal(envelope.ExperimentId, selected.EvidenceExperimentId);
        Assert.Equal(MetricContract.MillisecondsUnit, selected.Unit);
    }

    // --- manifest -----------------------------------------------------------

    [Fact]
    public void Manifest_covers_all_nine_and_is_byte_stable_for_the_same_input()
    {
        string first = BenchmarkManifestWriter.Write(
            Suite().SyntheticResults.Concat(Suite().ProductionResults).ToList());
        string second = BenchmarkManifestWriter.Write(
            Suite().SyntheticResults.Concat(Suite().ProductionResults).ToList());

        Assert.Equal(first, second);

        foreach (string benchmarkId in BenchmarkCatalogue.AllBenchmarkIds)
        {
            Assert.Contains("\"benchmark_id\": \"" + benchmarkId + "\"", first);
        }

        Assert.Contains("\"percentile_methodology\"", first);
        Assert.Contains("\"decision_owner\"", first);
        Assert.Contains("\"snapshot_hash\"", first);
        Assert.Contains("CapabilityUnavailable", first);
    }

    [Fact]
    public void Manifest_records_a_refusal_reason_rather_than_a_null_success()
    {
        BenchmarkEnvelope envelope = Runner().Run(
            BenchmarkFixtures.Production("B-06"),
            BenchmarkSuite.RequestFor("B-06", TestCommit, TestCapturedAt));

        string manifest = BenchmarkManifestWriter.Write(new[] { envelope });

        Assert.Contains("\"terminal_state\": \"CapabilityUnavailable\"", manifest);
        Assert.Contains("\"measurements\": []", manifest);
        Assert.Contains("\"production_measured\": false", manifest);
        Assert.DoesNotContain("\"refusal_reason\": null", manifest);
    }

    [Fact]
    public void Manifest_states_not_applicable_rather_than_zero_for_absent_percentiles()
    {
        string manifest = BenchmarkManifestWriter.Write(new[]
        {
            Runner().Run(
                BenchmarkFixtures.Synthetic("B-01"),
                BenchmarkSuite.RequestFor("B-01", TestCommit, TestCapturedAt))
        });

        Assert.Contains("\"p99\"", manifest);
        Assert.DoesNotContain("\"p95\": \"0\"", manifest);
    }

    // --- isolation ----------------------------------------------------------

    [Fact]
    public void The_harness_assembly_references_no_application_infrastructure_or_web_lane()
    {
        string[] forbidden =
        {
            "PlantProcess.Application", "PlantProcess.Infrastructure", "PlantProcess.Api",
            "PlantProcess.Domain", "PlantProcess.Analytics",
            "Microsoft.EntityFrameworkCore", "Microsoft.Extensions.DependencyInjection",
            "Microsoft.AspNetCore", "Npgsql"
        };

        List<string> referenced = typeof(BenchmarkRunner).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .ToList();

        foreach (string prefix in forbidden)
        {
            Assert.DoesNotContain(
                referenced, name => name.StartsWith(prefix, StringComparison.Ordinal));
        }
    }

    [Fact]
    public void The_harness_types_all_live_in_the_ml_runtime_assembly()
    {
        System.Reflection.Assembly expected = typeof(BenchmarkRunner).Assembly;

        Assert.Same(expected, typeof(BenchmarkCatalogue).Assembly);
        Assert.Same(expected, typeof(BenchmarkEnvelope).Assembly);
        Assert.Same(expected, typeof(SelectedValueGuard).Assembly);
        Assert.Same(expected, typeof(BenchmarkManifestWriter).Assembly);
        Assert.Same(expected, typeof(BenchmarkFixtures).Assembly);
    }

    [Fact]
    public void The_harness_registers_itself_nowhere()
    {
        string[] registrationShapes = { "Extensions", "Startup", "Registration", "Module", "Installer" };

        IEnumerable<Type> harnessTypes = typeof(BenchmarkRunner).Assembly
            .GetTypes()
            .Where(t => t.Namespace is not null
                        && t.Namespace.EndsWith(".Benchmarks", StringComparison.Ordinal));

        Assert.NotEmpty(harnessTypes);

        foreach (Type type in harnessTypes)
        {
            foreach (string shape in registrationShapes)
            {
                Assert.DoesNotContain(shape, type.Name, StringComparison.Ordinal);
            }
        }
    }

    // --- helpers ------------------------------------------------------------

    private static BenchmarkEnvelope MeasuredEnvelope()
    {
        return Runner().Run(
            BenchmarkFixtures.Synthetic("B-01"),
            BenchmarkSuite.RequestFor("B-01", TestCommit, TestCapturedAt));
    }

    private static SubjectIdentity FixtureIdentity(string kind, string identity)
    {
        return new SubjectIdentity
        {
            Kind = kind,
            Identity = identity,
            Version = "1",
            FixtureKind = SubjectFixtureKind.DeterministicFixture
        };
    }

    private sealed class HostileSubject : IBenchmarkSubject
    {
        private readonly MetricValue _metric;

        public HostileSubject(string benchmarkId, MetricValue metric)
        {
            BenchmarkId = benchmarkId;
            _metric = metric;
            Identity = FixtureIdentity(BenchmarkCatalogue.Get(benchmarkId).SubjectKind, "hostile");
        }

        public string BenchmarkId { get; }

        public SubjectIdentity Identity { get; }

        public MeasurementScope Scope => MeasurementScope.Synthetic;

        public bool IsAvailable(out string unavailableReason, out string ownerTrigger)
        {
            unavailableReason = string.Empty;
            ownerTrigger = string.Empty;
            return true;
        }

        public IReadOnlyList<MetricValue> ExecuteSample(WorkloadSpec workload, int iterationIndex)
        {
            return new[] { _metric };
        }

        public IReadOnlyList<MetricValue> ScalarMetrics(WorkloadSpec workload)
        {
            return Array.Empty<MetricValue>();
        }
    }

    private sealed class SilentSubject : IBenchmarkSubject
    {
        public SilentSubject(string benchmarkId)
        {
            BenchmarkId = benchmarkId;
            Identity = FixtureIdentity(BenchmarkCatalogue.Get(benchmarkId).SubjectKind, "silent");
        }

        public string BenchmarkId { get; }

        public SubjectIdentity Identity { get; }

        public MeasurementScope Scope => MeasurementScope.Synthetic;

        public bool IsAvailable(out string unavailableReason, out string ownerTrigger)
        {
            unavailableReason = string.Empty;
            ownerTrigger = string.Empty;
            return true;
        }

        public IReadOnlyList<MetricValue> ExecuteSample(WorkloadSpec workload, int iterationIndex)
        {
            return Array.Empty<MetricValue>();
        }

        public IReadOnlyList<MetricValue> ScalarMetrics(WorkloadSpec workload)
        {
            return Array.Empty<MetricValue>();
        }
    }

    private sealed class ThrowingSubject : IBenchmarkSubject
    {
        public ThrowingSubject(string benchmarkId)
        {
            BenchmarkId = benchmarkId;
            Identity = FixtureIdentity(BenchmarkCatalogue.Get(benchmarkId).SubjectKind, "throwing");
        }

        public string BenchmarkId { get; }

        public SubjectIdentity Identity { get; }

        public MeasurementScope Scope => MeasurementScope.Synthetic;

        public bool IsAvailable(out string unavailableReason, out string ownerTrigger)
        {
            unavailableReason = string.Empty;
            ownerTrigger = string.Empty;
            return true;
        }

        public IReadOnlyList<MetricValue> ExecuteSample(WorkloadSpec workload, int iterationIndex)
        {
            throw new InvalidOperationException("deliberate fixture failure");
        }

        public IReadOnlyList<MetricValue> ScalarMetrics(WorkloadSpec workload)
        {
            return Array.Empty<MetricValue>();
        }
    }

    /// <summary>
    /// Exists only so the guard can be falsified in both directions. It declares
    /// itself a real production subject inside a test assembly; nothing in the
    /// product can construct one.
    /// </summary>
    private sealed class RealSubjectForGuardTest : IBenchmarkSubject
    {
        public RealSubjectForGuardTest(string benchmarkId)
        {
            BenchmarkId = benchmarkId;
            Identity = new SubjectIdentity
            {
                Kind = BenchmarkCatalogue.Get(benchmarkId).SubjectKind,
                Identity = "test-only",
                Version = "1",
                FixtureKind = SubjectFixtureKind.RealSubject
            };
        }

        public string BenchmarkId { get; }

        public SubjectIdentity Identity { get; }

        public MeasurementScope Scope => MeasurementScope.Production;

        public bool IsAvailable(out string unavailableReason, out string ownerTrigger)
        {
            unavailableReason = string.Empty;
            ownerTrigger = string.Empty;
            return true;
        }

        public IReadOnlyList<MetricValue> ExecuteSample(WorkloadSpec workload, int iterationIndex)
        {
            return new[]
            {
                MetricValue.Create(
                    "time_to_first_token", MetricFamily.Latency, 100.0 + iterationIndex,
                    MetricContract.MillisecondsUnit, MetricAggregation.Sample)
            };
        }

        public IReadOnlyList<MetricValue> ScalarMetrics(WorkloadSpec workload)
        {
            return Array.Empty<MetricValue>();
        }
    }
}
