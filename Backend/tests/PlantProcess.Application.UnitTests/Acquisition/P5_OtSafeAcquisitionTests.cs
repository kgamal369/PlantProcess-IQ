using PlantProcess.Application.Acquisition;
using Xunit;

namespace PlantProcess.Application.UnitTests.Acquisition;

public sealed class P5_OtSafeAcquisitionTests
{
    [Fact]
    public void T028_collector_registration_requires_one_way_no_inbound_no_control()
    {
        var safe = new OtSafeEdgeCollectorRegistration(
            CollectorId: Guid.NewGuid(),
            TenantId: Guid.NewGuid(),
            CollectorCode: "EDGE-01",
            SiteCode: "SITE-A",
            SourceSystemCode: "HIST-A",
            CredentialFingerprint: "fingerprint-123",
            OneWayPushOnly: true,
            NoInboundNetworkPath: true,
            NoControlPath: true,
            BufferRetentionHours: 72);

        Assert.True(safe.IsOtSafe);

        var unsafeCollector = safe with { NoInboundNetworkPath = false };

        Assert.False(unsafeCollector.IsOtSafe);
    }

    [Fact]
    public void T028_gateway_accepts_valid_push_and_rejects_duplicate_rows()
    {
        var tenantId = Guid.NewGuid();
        var collectorId = Guid.NewGuid();
        var collector = new OtSafeEdgeCollectorRegistration(
            collectorId,
            tenantId,
            "EDGE-01",
            "SITE-A",
            "HIST-A",
            "fingerprint-123",
            OneWayPushOnly: true,
            NoInboundNetworkPath: true,
            NoControlPath: true,
            BufferRetentionHours: 72);

        var batch = new EdgeCollectorPushBatch(
            collectorId,
            tenantId,
            "HIST-A",
            "tag_history",
            "BATCH-001",
            DateTimeOffset.Parse("2026-06-04T08:00:00Z"),
            DateTimeOffset.Parse("2026-06-04T08:05:00Z"),
            new[]
            {
                new EdgeCollectorPushRow(1, DateTimeOffset.Parse("2026-06-04T08:01:00Z"), "TEMP_1", "C", 101.4m, null, "Good", "{}"),
                new EdgeCollectorPushRow(2, DateTimeOffset.Parse("2026-06-04T08:02:00Z"), "TEMP_1", "C", 102.1m, null, "Good", "{}")
            },
            Signature: "signed");

        var accepted = new OtSafeEdgeCollectorGateway().Accept(collector, batch, "signed");

        Assert.True(accepted.Accepted);
        Assert.Equal(2, accepted.AcceptedRows);

        var duplicate = batch with
        {
            Rows =
            [
                batch.Rows[0],
                batch.Rows[0]
            ]
        };

        var rejected = new OtSafeEdgeCollectorGateway().Accept(collector, duplicate, "signed");

        Assert.False(rejected.Accepted);
        Assert.Contains(rejected.Errors, x => x.Contains("duplicate row numbers", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void T029_historian_mapper_maps_tag_history_to_canonical_parameter_observations()
    {
        var observations = new[]
        {
            new HistorianTagObservation("TEMP_1", DateTimeOffset.Parse("2026-06-04T08:00:00Z"), 101.5m, null, "C", "good", "SRC-1"),
            new HistorianTagObservation("IGNORED_TAG", DateTimeOffset.Parse("2026-06-04T08:01:00Z"), 999m, null, "C", "good", "SRC-2")
        };

        var mappings = new[]
        {
            new HistorianTagMapping("TEMP_1", "process.temperature", "degC", IsActive: true)
        };

        var drafts = new HistorianTagHistoryMapper().Map(observations, mappings);

        Assert.Single(drafts);
        Assert.Equal("process.temperature", drafts[0].ParameterCode);
        Assert.Equal("degC", drafts[0].Unit);
        Assert.Equal("Good", drafts[0].QualityFlag);
        Assert.Equal("SRC-1", drafts[0].SourceRecordId);
    }

    [Fact]
    public void T030_schema_drift_detects_removed_type_changed_unit_changed_and_added_fields()
    {
        var expected = new[]
        {
            new SchemaFieldSnapshot("tag_name", "text", null, IsRequired: true),
            new SchemaFieldSnapshot("value", "numeric", "C", IsRequired: true),
            new SchemaFieldSnapshot("quality", "text", null, IsRequired: false),
            new SchemaFieldSnapshot("required_missing", "text", null, IsRequired: true)
        };

        var actual = new[]
        {
            new SchemaFieldSnapshot("tag_name", "text", null, IsRequired: true),
            new SchemaFieldSnapshot("value", "text", "F", IsRequired: true),
            new SchemaFieldSnapshot("new_optional", "text", null, IsRequired: false)
        };

        var findings = new SchemaDriftDetector().Detect(expected, actual);

        Assert.Contains(findings, x => x.FieldName == "required_missing" && x.DriftType == "Removed" && x.BlocksIngestion);
        Assert.Contains(findings, x => x.FieldName == "value" && x.DriftType == "TypeChanged" && x.BlocksIngestion);
        Assert.Contains(findings, x => x.FieldName == "value" && x.DriftType == "UnitChanged" && !x.BlocksIngestion);
        Assert.Contains(findings, x => x.FieldName == "new_optional" && x.DriftType == "Added" && !x.BlocksIngestion);
    }

    [Fact]
    public void T032_network_proof_contains_no_inbound_and_no_control_language()
    {
        var status = new OtSafeCollectorStatus(
            CollectorId: Guid.NewGuid(),
            CollectorCode: "EDGE-01",
            SiteCode: "SITE-A",
            LastPushAtUtc: DateTimeOffset.UtcNow,
            LagSeconds: 12,
            BufferedBatchCount: 0,
            BufferedRowCount: 0,
            HealthStatus: "Healthy",
            NetworkProof: "one-way-push-only/no-inbound-piq-to-ot/no-control-path",
            CredentialStatus: "active");

        Assert.True(status.IsHealthy);
        Assert.Contains("no-inbound", status.NetworkProof);
        Assert.Contains("no-control", status.NetworkProof);
    }
}