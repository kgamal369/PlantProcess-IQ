using FluentAssertions;
using PlantProcess.Application.Acquisition;
using PlantProcess.Application.Integration.Services.SourceSystems;
using Xunit;

namespace PlantProcess.Application.UnitTests.Integration;

public sealed class SchemaDriftWiringTests
{
    private static SchemaFieldSnapshot Field(string name, string type, bool required = true)
        => new(name, type, null, required);

    [Fact]
    public void Detect_FlagsRemovedColumn_ByName()
    {
        var expected = new[] { Field("Id", "int"), Field("Temperature", "numeric") };
        var actual = new[] { Field("Id", "int") };

        var findings = Detector().Detect(expected, actual);

        findings.Should().Contain(f => f.FieldName == "Temperature");
    }

    [Fact]
    public void Detect_FlagsTypeChange_ByName()
    {
        var expected = new[] { Field("Id", "int") };
        var actual = new[] { Field("Id", "bigint") };

        var findings = Detector().Detect(expected, actual);

        findings.Should().Contain(f => f.FieldName == "Id");
    }

    [Fact]
    public void Detect_NoDrift_WhenIdentical()
    {
        var fields = new[] { Field("Id", "int"), Field("Ts", "timestamp") };

        var findings = Detector().Detect(fields, fields);

        findings.Should().BeEmpty();
    }

    [Theory]
    [InlineData("{\"columns\":[{\"source\":\"Temperature\",\"target\":\"temp_c\"}]}", "Temperature", true)]
    [InlineData("{\"columns\":[{\"source\":\"Temperature\",\"target\":\"temp_c\"}]}", "Pressure", false)]
    [InlineData("{\"Temperature\":\"x\"}", "temperature", true)]
    [InlineData("plain-text-Temperature", "Temperature", true)]
    [InlineData("", "Temperature", false)]
    public void MappingReferencesField_DetectsReference(string json, string field, bool expected)
    {
        ConnectorSchemaDriftService.MappingReferencesField(json, field).Should().Be(expected);
    }

    private static SchemaDriftDetector Detector() => new();
}