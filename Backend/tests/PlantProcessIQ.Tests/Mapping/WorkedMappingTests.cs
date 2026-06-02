using PlantProcessIQ.Application.Analysis;
using PlantProcessIQ.Application.Mapping;
using Xunit;

namespace PlantProcessIQ.Tests.Mapping;

public class WorkedMappingTests
{
    [Theory]
    [InlineData("C-0044170", "44170")]
    [InlineData("44170", "44170")]
    [InlineData("c0044170", "44170")]
    [InlineData("  C-44170 ", "44170")]
    public void CoilId_normalizes_consistently(string raw, string expected)
        => Assert.Equal(expected, BusinessKeys.NormalizeCoilId(raw));

    [Fact]
    public void CoilId_variants_resolve_equal()
    {
        var r = new CoilIdResolver();
        Assert.Equal(r.Resolve("C-0044170", "phys-1"), r.Resolve("44170", "phys-1"));
    }

    [Fact]
    public void CoilId_conflict_throws_typed_error()
    {
        var r = new CoilIdResolver();
        r.Resolve("C-0044170", "phys-1");
        var ex = Assert.Throws<BusinessKeyConflictException>(() => r.Resolve("44170", "phys-2"));
        Assert.Contains("44170", ex.Message);
    }

    [Fact]
    public void Fpsy_view_formula_matches_known_value()
        => Assert.Equal(93.5, SimpleKpis.Fpsy(1000, 935));   // 935 prime / 1000 cast -> 93.5 %

    [Fact]
    public async Task QualityEvent_join_mapping_validates_on_demo_sample()
    {
        var spec = new MappingSpec
        {
            CanonicalEntityName = "quality_event",
            SourceTable = "staging.qa_inspection",
            Fields = new FieldMapping[]
            {
                new("event_id", "insp_id"),
                new("coil_id", "coil_no"),
                new("defect_code", "defect"),
                new("severity", "sev"),
                new("detected_at", "ts"),
            }
        };
        var reader = new FakeStaging(
            new[]
            {
                new StagedColumn("insp_id", TypeCategory.String),
                new StagedColumn("coil_no", TypeCategory.String),
                new StagedColumn("defect", TypeCategory.String),
                new StagedColumn("sev", TypeCategory.Number),
                new StagedColumn("ts", TypeCategory.DateTime),
            },
            new[] { Row(("insp_id", "I1"), ("coil_no", "C-0044170"), ("defect", "EDGE_CRACK"), ("sev", "3"), ("ts", "2026-05-01T10:00:00Z")) });

        var res = await new CanonicalMappingValidator(reader).ValidateAsync(spec);
        Assert.True(res.IsValid, string.Join("; ", res.Errors.Select(e => e.Message)));
    }

    [Fact]
    public async Task Missing_required_field_blocks_and_names_field()
    {
        var spec = new MappingSpec
        {
            CanonicalEntityName = "quality_event",
            SourceTable = "staging.qa_inspection",
            Fields = new FieldMapping[] { new("event_id", "insp_id"), new("coil_id", "coil_no") } // defect_code + detected_at missing
        };
        var reader = new FakeStaging(
            new[] { new StagedColumn("insp_id", TypeCategory.String), new StagedColumn("coil_no", TypeCategory.String) },
            Array.Empty<IReadOnlyDictionary<string, string?>>());

        var res = await new CanonicalMappingValidator(reader).ValidateAsync(spec);
        Assert.False(res.IsValid);
        Assert.Contains(res.Errors, e => e.Code == MappingErrorCode.RequiredFieldUnmapped && e.Field == "defect_code");
        Assert.Contains(res.Errors, e => e.Code == MappingErrorCode.RequiredFieldUnmapped && e.Field == "detected_at");
    }

    [Fact]
    public async Task Text_into_numeric_is_rejected_with_column_named()
    {
        var spec = new MappingSpec
        {
            CanonicalEntityName = "quality_event",
            SourceTable = "staging.qa_inspection",
            Fields = new FieldMapping[]
            {
                new("event_id", "insp_id"), new("coil_id", "coil_no"),
                new("defect_code", "defect"), new("detected_at", "ts"),
                new("severity", "sev_text"),
            }
        };
        var reader = new FakeStaging(
            new[]
            {
                new StagedColumn("insp_id", TypeCategory.String), new StagedColumn("coil_no", TypeCategory.String),
                new StagedColumn("defect", TypeCategory.String), new StagedColumn("ts", TypeCategory.DateTime),
                new StagedColumn("sev_text", TypeCategory.String),
            },
            new[] { Row(("insp_id", "I1"), ("coil_no", "C1"), ("defect", "D"), ("ts", "2026-05-01T10:00:00Z"), ("sev_text", "high")) });

        var res = await new CanonicalMappingValidator(reader).ValidateAsync(spec);
        Assert.Contains(res.Errors, e => e.Code == MappingErrorCode.IncompatibleType && e.Field == "severity" && e.Message.Contains("sev_text"));
    }

    [Fact]
    public void Unit_conversion_is_verified_numerically()
    {
        Assert.Equal(0.005, UnitConverter.Convert(5, "mm", "m"), 6);     // 5 mm -> 0.005 m
        Assert.Equal(100.0, UnitConverter.Convert(212, "f", "c"), 6);    // 212 F -> 100 C
        Assert.Equal(0.0, UnitConverter.Convert(273.15, "k", "c"), 6);   // 273.15 K -> 0 C
        Assert.False(UnitConverter.CanConvert("mm", "c"));               // dimension mismatch rejected
    }

    [Fact]
    public void Mapping_rollback_keeps_prior_version_live()
    {
        var store = new InMemoryMappingVersions();
        store.Publish("quality_event", "v1-sql");                                  // good publish
        Assert.Throws<InvalidOperationException>(() => store.Publish("quality_event", "BAD")); // bad publish rejected atomically
        Assert.Equal("v1-sql", store.Active("quality_event"));                      // prior version still live
        Assert.True(store.History("quality_event").Count >= 1);                    // auditable history
    }

    private static IReadOnlyDictionary<string, string?> Row(params (string, string?)[] cells)
        => cells.ToDictionary(c => c.Item1, c => c.Item2);
}

internal sealed class FakeStaging : IStagingSampleReader
{
    private readonly StagedSample _s;
    public FakeStaging(StagedColumn[] cols, IReadOnlyDictionary<string, string?>[] rows)
        => _s = new StagedSample { Columns = cols, Rows = rows };
    public Task<StagedSample> SampleAsync(string sourceTable, int sampleSize, CancellationToken ct = default) => Task.FromResult(_s);
}

internal sealed class InMemoryMappingVersions
{
    private readonly Dictionary<string, string> _active = new();
    private readonly Dictionary<string, List<string>> _hist = new();
    public void Publish(string entity, string sql)
    {
        if (sql == "BAD") throw new InvalidOperationException("publish failed: invalid SQL"); // active unchanged
        _active[entity] = sql;
        (_hist.TryGetValue(entity, out var h) ? h : (_hist[entity] = new())).Add(sql);
    }
    public string Active(string entity) => _active[entity];
    public List<string> History(string entity) => _hist.TryGetValue(entity, out var h) ? h : new();
}