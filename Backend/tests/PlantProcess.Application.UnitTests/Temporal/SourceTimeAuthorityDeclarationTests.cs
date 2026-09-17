using PlantProcess.Analytics.Core.Kernel;
using PlantProcess.Application.Temporal;

namespace PlantProcess.Application.UnitTests.Temporal;

/// <summary>
/// The persistence contract answers exactly what the frozen kernel answers.
/// Backlog reference: T-233.
/// </summary>
public sealed class SourceTimeAuthorityDeclarationTests
{
    private static readonly DateTime From = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static SourceTimeAuthorityDeclarationRequest Request(
        string? source = "SRC-A",
        string? signal = "event_time",
        string? role = "Effective",
        string? origin = "DeclaredFixedOffset",
        TimeSpan? fixedOffset = null,
        string? zone = null,
        TimeSpan? resolution = null,
        TimeSpan? skew = null,
        string? convention = "ResolutionIsHalfWidth",
        DateTime? to = null) =>
        new(source, signal, role, origin,
            fixedOffset ?? TimeSpan.FromHours(2),
            zone,
            resolution ?? TimeSpan.FromMilliseconds(500),
            skew ?? TimeSpan.FromSeconds(1),
            convention,
            From, to, null, null, null);

    [Fact]
    public void A_lawful_request_normalises_to_the_kernel_value_with_trimmed_keys()
    {
        var ok = SourceTimeAuthorityDeclarations.TryNormalise(
            Request(source: "  SRC-A ", signal: " event_time  "), out var declaration, out var code);

        Assert.True(ok, code);
        Assert.NotNull(declaration);
        Assert.Equal(
            new TimeSignalDeclaration("SRC-A", "event_time", TimeRole.Effective, TimeOffsetOrigin.DeclaredFixedOffset,
                TimeSpan.FromHours(2), string.Empty, TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(1),
                TimeUncertaintyConvention.ResolutionIsHalfWidth),
            declaration);
    }

    [Fact]
    public void Keys_keep_their_case()
    {
        Assert.True(SourceTimeAuthorityDeclarations.TryNormalise(Request(source: "Src-a"), out var declaration, out _));
        Assert.Equal("Src-a", declaration!.SourceKey);
    }

    [Fact]
    public void An_undeclared_offset_origin_is_the_kernel_refusal()
    {
        Assert.False(SourceTimeAuthorityDeclarations.TryNormalise(Request(origin: "Undeclared"), out _, out var code));
        Assert.Equal(SourceTimeCodes.OffsetNotDeclared, code);
    }

    [Fact]
    public void A_zone_rule_without_a_zone_key_is_refused()
    {
        Assert.False(SourceTimeAuthorityDeclarations.TryNormalise(
            Request(origin: "DeclaredZoneRule", fixedOffset: TimeSpan.Zero, zone: "  "), out _, out var code));
        Assert.Equal(SourceTimeCodes.ZoneNotDeclared, code);
    }

    [Fact]
    public void A_zone_key_beside_a_fixed_offset_is_a_conflict()
    {
        Assert.False(SourceTimeAuthorityDeclarations.TryNormalise(Request(zone: "zone-a"), out _, out var code));
        Assert.Equal(SourceTimeCodes.OffsetDeclarationConflict, code);
    }

    [Fact]
    public void A_negative_clock_quality_is_refused()
    {
        Assert.False(SourceTimeAuthorityDeclarations.TryNormalise(
            Request(skew: TimeSpan.FromSeconds(-1)), out _, out var code));
        Assert.Equal(SourceTimeCodes.TimeQualityNotDeclared, code);
    }

    [Fact]
    public void A_blank_key_is_refused()
    {
        Assert.False(SourceTimeAuthorityDeclarations.TryNormalise(Request(signal: " "), out _, out var code));
        Assert.Equal(SourceTimeCodes.SignalNotDeclared, code);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("effective")]
    [InlineData("")]
    [InlineData(null)]
    public void A_role_that_is_not_an_exact_member_name_is_refused(string? role)
    {
        Assert.False(SourceTimeAuthorityDeclarations.TryNormalise(Request(role: role), out _, out var code));
        Assert.Equal(SourceTimeCodes.RoleNotAuthorised, code);
    }

    [Fact]
    public void A_numeric_convention_is_refused()
    {
        Assert.False(SourceTimeAuthorityDeclarations.TryNormalise(Request(convention: "0"), out _, out var code));
        Assert.Equal(SourceTimeCodes.TimeQualityNotDeclared, code);
    }

    [Fact]
    public void An_effective_window_that_ends_before_it_starts_is_refused()
    {
        Assert.False(SourceTimeAuthorityDeclarations.TryNormalise(Request(to: From), out _, out var code));
        Assert.Equal(SourceTimeAuthorityPersistenceCodes.EffectiveWindowInvalid, code);
    }

    [Fact]
    public void The_response_carries_the_kernel_uncertainty()
    {
        Assert.True(SourceTimeAuthorityDeclarations.TryNormalise(
            Request(convention: "ResolutionIsQuantisationStep", resolution: TimeSpan.FromSeconds(1),
                skew: TimeSpan.FromSeconds(2)),
            out var declaration, out _));

        var response = new PersistedTimeSignalDeclaration(Guid.NewGuid(), declaration!, From, null).ToResponse();

        Assert.Equal(TimeSpan.FromMilliseconds(2500), response.Uncertainty);
        Assert.Equal(declaration!.Uncertainty, response.Uncertainty);
        Assert.Equal("ResolutionIsQuantisationStep", response.UncertaintyConvention);
        Assert.Equal(string.Empty, response.ZoneKey);
    }

    [Fact]
    public void A_registry_built_from_persisted_rows_resolves_through_the_kernel()
    {
        Assert.True(SourceTimeAuthorityDeclarations.TryNormalise(Request(), out var declaration, out _));

        var registry = SourceTimeAuthorityDeclarations.ToRegistry(new[]
        {
            new PersistedTimeSignalDeclaration(Guid.NewGuid(), declaration!, From, null)
        });

        var resolution = SourceTimeAuthorityKernel.Resolve(
            registry, "SRC-A", "event_time",
            RawSourceTime.WithoutOffset(new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Unspecified)));

        Assert.True(resolution.IsResolved, resolution.Code);
        Assert.Equal(new DateTimeOffset(2026, 3, 1, 10, 0, 0, TimeSpan.FromHours(2)), resolution.Instant!.Instant);
        Assert.Equal(TimeRole.Effective, resolution.Instant.Role);
    }

    [Fact]
    public void A_stored_row_the_contract_refuses_fails_the_load_closed()
    {
        var unlawful = new TimeSignalDeclaration(
            "SRC-A", "event_time", TimeRole.Effective, TimeOffsetOrigin.Undeclared,
            TimeSpan.Zero, string.Empty, TimeSpan.Zero, TimeSpan.Zero,
            TimeUncertaintyConvention.ResolutionIsHalfWidth);

        Assert.Throws<InvalidOperationException>(() => SourceTimeAuthorityDeclarations.ToRegistry(new[]
        {
            new PersistedTimeSignalDeclaration(Guid.NewGuid(), unlawful, From, null)
        }));
    }

    [Fact]
    public void Offsetless_value_uses_the_declared_fixed_offset_instead_of_becoming_UTC()
    {
        Assert.True(SourceTimeAuthorityDeclarations.TryNormalise(Request(), out var declaration, out _));
        var registry = SourceTimeAuthorityDeclarations.ToRegistry(new[] { new PersistedTimeSignalDeclaration(Guid.NewGuid(), declaration!, From, null) });
        var result = SourceTimeRuntimeResolver.Resolve(registry, "SRC-A", "event_time", "2026-03-01 12:00:00", TimeRole.Effective);
        Assert.True(result.IsResolved, result.Detail);
        Assert.Equal(new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc), result.UtcValue);
    }

    [Fact]
    public void Missing_declaration_is_ST01_and_never_a_machine_timezone_fallback()
    {
        var result = SourceTimeRuntimeResolver.Resolve(new SourceTimeAuthorityRegistry(), "SRC-X", "event_time", "2026-03-01 12:00:00", TimeRole.Effective);
        Assert.False(result.IsResolved);
        Assert.Equal(SourceTimeCodes.SignalNotDeclared, result.Code);
        Assert.False(result.IsSyntaxFailure);
    }

    [Fact]
    public void Malformed_raw_timestamp_is_syntax_failure_not_an_invented_ST_code()
    {
        Assert.True(SourceTimeAuthorityDeclarations.TryNormalise(Request(), out var declaration, out _));
        var registry = SourceTimeAuthorityDeclarations.ToRegistry(new[] { new PersistedTimeSignalDeclaration(Guid.NewGuid(), declaration!, From, null) });
        var result = SourceTimeRuntimeResolver.Resolve(registry, "SRC-A", "event_time", "not-a-time", TimeRole.Effective);
        Assert.False(result.IsResolved);
        Assert.True(result.IsSyntaxFailure);
        Assert.Equal(string.Empty, result.Code);
    }

}