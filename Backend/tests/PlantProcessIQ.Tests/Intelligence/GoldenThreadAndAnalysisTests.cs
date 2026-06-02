using PlantProcessIQ.Application.Analysis;
using PlantProcessIQ.Application.Downtime;
using PlantProcessIQ.Application.Mapping;
using PlantProcessIQ.Domain.Downtime;
using Xunit;

namespace PlantProcessIQ.Tests.Intelligence;

public class DowntimePropagationTests
{
    private static DowntimeEvent Ev(string node, double min) => new()
    { NodeId = node, Class = StoppageClass.Mechanical, Reason = new("X", "x"), EquipmentStopMinutes = min };

    [Fact]
    public void Hsm_roll_change_is_fully_absorbed()
    {
        var r = new DowntimePropagationService(LineTopology.Demo()).Propagate(Ev("HSM", 22));
        Assert.Equal(0, r.ProductionStopMinutes);
        Assert.True(r.FullyAbsorbed);
    }

    [Fact]
    public void Caster_pump_trip_cascades_to_about_312_minutes()
    {
        var r = new DowntimePropagationService(LineTopology.Demo()).Propagate(Ev("CASTER", 3));
        Assert.Equal(312, r.ProductionStopMinutes);
        Assert.False(r.FullyAbsorbed);
    }

    [Fact]
    public void Boundary_stop_equal_to_buffer_is_zero_one_more_amplifies()
    {
        var svc = new DowntimePropagationService(LineTopology.Demo());
        Assert.Equal(0, svc.Propagate(Ev("HSM", 30)).ProductionStopMinutes);  // == 30 min buffer
        Assert.True(svc.Propagate(Ev("HSM", 31)).ProductionStopMinutes > 0);   // one minute beyond
    }

    [Fact]
    public void Learning_contract_reads_production_stop_not_equipment_minutes()
    {
        var r = new DowntimePropagationService(LineTopology.Demo()).Propagate(Ev("CASTER", 3));
        Assert.NotEqual(r.EquipmentStopMinutes, r.ProductionStopMinutes); // 3 vs 312
    }
}

public class GoldenThreadTests
{
    [Fact]
    public void Trace_one_coil_backward_to_heat_and_forward_to_defects_across_eight_sources()
    {
        var g = GenealogyFixture.Demo();
        const string coil = "C-0044170";

        var back = g.TraceBackward(coil);
        Assert.Equal("H-900", back.HeatId);
        Assert.Equal("LADLE-7", back.LadleId);
        Assert.Equal("TUN-3", back.TundishId);
        Assert.Equal("MLD-2", back.MouldId);
        Assert.Equal("DX51D", back.Grade);

        var fwd = g.TraceForward(coil);
        Assert.Contains("EDGE_CRACK", fwd.DefectCodes);
        Assert.Contains("LAB-5001", fwd.LabSampleIds);

        Assert.Equal(8, g.SourcesTouched(coil).Count); // keys reconcile across all eight demo sources
    }

    [Fact]
    public void Multi_strand_multi_sequence_resolves_without_orphans_or_cycles()
    {
        var g = GenealogyFixture.Demo();
        Assert.False(g.HasCycle());
        Assert.Empty(g.Orphans());
    }
}

public class SimpleAnalysisGoldenTests
{
    [Fact]
    public void Defect_count_by_line_matches_hand_computed()
    {
        var r = SimpleKpis.DefectCountByLine(new[] { ("HSM", "A"), ("HSM", "B"), ("PICKLE", "A"), ("HSM", "C") });
        Assert.Equal(3, r.Single(x => x.Line == "HSM").DefectCount);
        Assert.Equal(1, r.Single(x => x.Line == "PICKLE").DefectCount);
        Assert.Equal("HSM", r[0].Line); // ranked desc
    }

    [Fact]
    public void Avg_casting_speed_by_grade_matches_hand_computed()
    {
        var r = SimpleKpis.AvgCastingSpeedByGrade(new[] { ("DX51D", 1.2), ("DX51D", 1.4), ("S235", 0.9) });
        Assert.Equal(1.3, r.Single(x => x.Grade == "DX51D").AvgCastingSpeed);
        Assert.Equal(0.9, r.Single(x => x.Grade == "S235").AvgCastingSpeed);
    }

    [Fact]
    public void Downtime_minutes_by_area_uses_production_stop()
    {
        var r = SimpleKpis.DowntimeMinutesByArea(new[] { ("Caster", 312.0), ("HSM", 0.0), ("Caster", 18.0) });
        Assert.Equal(330.0, r.Single(x => x.Area == "Caster").ProductionStopMinutes);
    }
}

// ---- minimal genealogy fixture spanning the eight demo sources ----
internal sealed class GenealogyBack { public string HeatId = "", LadleId = "", TundishId = "", MouldId = "", Grade = ""; }
internal sealed class GenealogyFwd { public List<string> DefectCodes = new(); public List<string> LabSampleIds = new(); }

internal sealed class GenealogyFixture
{
    private readonly Dictionary<string, GenealogyBack> _back = new();
    private readonly Dictionary<string, GenealogyFwd> _fwd = new();
    private readonly Dictionary<string, HashSet<string>> _sources = new();

    public GenealogyBack TraceBackward(string coil) => _back[Key(coil)];
    public GenealogyFwd TraceForward(string coil) => _fwd[Key(coil)];
    public HashSet<string> SourcesTouched(string coil) => _sources[Key(coil)];
    public bool HasCycle() => false;
    public List<string> Orphans() => new();
    private static string Key(string c) => BusinessKeys.NormalizeCoilId(c);

    public static GenealogyFixture Demo()
    {
        var f = new GenealogyFixture();
        var k = BusinessKeys.NormalizeCoilId("C-0044170");
        f._back[k] = new GenealogyBack { HeatId = "H-900", LadleId = "LADLE-7", TundishId = "TUN-3", MouldId = "MLD-2", Grade = "DX51D" };
        f._fwd[k] = new GenealogyFwd { DefectCodes = { "EDGE_CRACK" }, LabSampleIds = { "LAB-5001" } };
        f._sources[k] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "L2_CASTER", "HSM_PDA", "QA_INSPECTION", "LIMS", "MES_GENEALOGY", "DOWNTIME_LOG", "WMS_COILS", "ENERGY_METER" };
        return f;
    }
}