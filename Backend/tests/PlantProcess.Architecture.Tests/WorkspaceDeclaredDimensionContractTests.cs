using System;
using System.Collections.Generic;
using System.IO;
using PlantProcess.Application.Dashboarding.Contracts;
using PlantProcess.Application.Dashboarding.Services.Dimensions;
using Xunit;

namespace PlantProcess.Architecture.Tests;

/// <summary>
/// T-094 part 2b, stage 2A: the workspace/global surface carries the same keyed
/// declared-dimension contract as the widget surface (design 4.5.13b). Proved
/// without a database: the query-string form parses to the same DTO the widget
/// surface accepts, malformed input is a typed refusal rather than a dropped
/// filter, and the contract and service sources carry the keyed path.
/// </summary>
[Trait("BacklogTask", "T-094")]
[Trait("Gate", "WorkspaceDeclaredDimensionContract")]
public sealed class WorkspaceDeclaredDimensionContractTests
{
    [Fact]
    public void A_query_string_entry_parses_to_the_keyed_filter_dto()
    {
        var parsed = DeclaredDimensionFilterQueryParser.Parse(new[] { "anyCode:some value" });

        var single = Assert.Single(parsed!);
        Assert.Equal("anyCode", single.Code);
        Assert.Equal("some value", single.Value);
    }

    [Fact]
    public void The_first_separator_splits_so_a_value_may_carry_the_separator()
    {
        var parsed = DeclaredDimensionFilterQueryParser.Parse(new[] { "code:a:b" });
        Assert.Equal("a:b", Assert.Single(parsed!).Value);
    }

    [Fact]
    public void Blank_entries_are_ignored_and_an_all_blank_list_is_no_filter()
    {
        Assert.Null(DeclaredDimensionFilterQueryParser.Parse(new[] { "", "   " }));
        Assert.Null(DeclaredDimensionFilterQueryParser.Parse(null));
        Assert.Null(DeclaredDimensionFilterQueryParser.Parse(Array.Empty<string>()));
    }

    [Theory]
    [InlineData("noSeparator")]
    [InlineData(":valueOnly")]
    [InlineData("codeOnly:")]
    [InlineData("9startsWithDigit:x")]
    [InlineData("has space:x")]
    public void A_malformed_entry_is_a_typed_refusal_never_a_dropped_filter(string entry)
    {
        var refusal = Assert.Throws<DimensionBindingRefusalException>(() =>
            DeclaredDimensionFilterQueryParser.Parse(new[] { entry }));

        Assert.Equal(DimensionBindingRefusalCodes.FilterMalformed, refusal.RefusalCode);
    }

    [Fact]
    public void A_posted_body_is_normalised_the_same_way()
    {
        var normalised = DeclaredDimensionFilterQueryParser.Normalise(new List<DeclaredDimensionFilterDto>
        {
            new(" code ", " v "),
            new("", "dropped"),
        });

        var single = Assert.Single(normalised!);
        Assert.Equal("code", single.Code);
        Assert.Equal("v", single.Value);
        Assert.Null(DeclaredDimensionFilterQueryParser.Normalise(null));
        Assert.Null(DeclaredDimensionFilterQueryParser.Normalise(new List<DeclaredDimensionFilterDto>()));
    }

    [Fact]
    public void The_workspace_query_contract_carries_the_keyed_set()
    {
        var query = new DashboardQueryDto(
            null, null, null, null, null, null, null, null, null, null, 1, 25, null, null,
            new[] { new DeclaredDimensionFilterDto("code", "value") });

        Assert.Single(query.DimensionFilters!);

        // Existing fourteen-argument construction still compiles and carries no filters.
        var legacy = new DashboardQueryDto(null, null, null, null, null, null, null, null, null, null, 1, 25, null, null);
        Assert.Null(legacy.DimensionFilters);
    }

    [Fact]
    public void Both_surfaces_resolve_declarations_through_the_same_binding_contract()
    {
        var root = ScopeAwareGenericity.RepositoryRoot();
        var workspace = File.ReadAllText(Path.Combine(root, "Backend", "PlantProcess.Application", "Dashboarding", "Services", "Queries", "DashboardQueryService.cs"));
        var widget = File.ReadAllText(Path.Combine(root, "Backend", "PlantProcess.Application", "Dashboarding", "Services", "Queries", "DashboardWidgetQueryService.cs"));

        foreach (var member in new[] { "BindsToSubject", "WhereDeclaredEquals", "SubjectKeysWhereDeclaredEqualsAsync", "RequireDeclaredDimensionAsync" })
        {
            Assert.Contains(member, workspace, StringComparison.Ordinal);
            Assert.Contains(member, widget, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Every_workspace_route_accepts_the_keyed_parameter()
    {
        var root = ScopeAwareGenericity.RepositoryRoot();
        var endpoints = File.ReadAllText(Path.Combine(root, "Backend", "PlantProcess.Api", "Endpoints", "Dashboarding", "DashboardEndpoints.cs"));

        var occurrences = 0;
        var index = 0;
        while ((index = endpoints.IndexOf("DeclaredDimensionFilterQueryParser.ParameterName", index, StringComparison.Ordinal)) >= 0)
        {
            occurrences++;
            index += 1;
        }

        // /overview, /quality, /risk, /data-quality, /materials.
        Assert.Equal(5, occurrences);
        Assert.Contains("AddEndpointFilter", endpoints, StringComparison.Ordinal);
    }
}