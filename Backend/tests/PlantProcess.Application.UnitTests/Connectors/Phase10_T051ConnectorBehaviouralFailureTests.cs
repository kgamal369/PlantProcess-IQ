using System;
using System.Collections.Generic;
using System.Linq;
using PlantProcess.Application.Connectors.Certification;
using Xunit;

namespace PlantProcess.Application.UnitTests.Connectors;

/// <summary>
/// PPIQ_REALIZATION_T051_CONNECTOR_BEHAVIOURAL_FAILURE_TESTS.
/// Per-connector behavioural and failure certification for CSV, Excel, MSSQL, and MySQL.
/// </summary>
public sealed class Phase10_T051ConnectorBehaviouralFailureTests
{
    private static readonly ConnectorBehaviourCertificationService Service = new();

    public static IEnumerable<object[]> CertifiedConnectors()
        => ConnectorCertificationScenarios.Phase10ConnectorSet.Select(x => new object[] { x });

    [Theory]
    [MemberData(nameof(CertifiedConnectors))]
    public void T051_Connector_TestBeforeSave_Then_Commits_SourceShaped_Staging(string providerType)
    {
        var scenario = ConnectorCertificationScenarios.ValidScenario(providerType);

        var result = Service.Certify(scenario);

        Assert.True(result.Passed, result.FailureReason);
        Assert.Equal(providerType, result.ProviderType);
        Assert.Equal("Completed", result.Batch.Status);
        Assert.Equal(2, result.Batch.RowCount);
        Assert.Equal(2, result.Staging.CommittedRowCount);
        Assert.Equal(scenario.SourceObjectName, result.Staging.SourceObjectName);
        Assert.Equal(scenario.SourceColumns, result.Staging.SourceColumns);
        Assert.Contains("test-before-save:passed", result.EvidenceLog);
        Assert.Contains("source-shaped-staging:committed", result.EvidenceLog);
    }

    [Theory]
    [MemberData(nameof(CertifiedConnectors))]
    public void T051_ReadBack_Profile_Masks_Secrets_And_Does_Not_Leak_Raw_Credential(string providerType)
    {
        var scenario = ConnectorCertificationScenarios.ValidScenario(providerType);

        var result = Service.Certify(scenario);
        var json = result.ToSafeReadBackJson();

        Assert.True(result.Passed, result.FailureReason);
        Assert.Equal("****", result.ReadBackProfile.SecretReferenceMask);
        Assert.NotEmpty(result.ReadBackProfile.SecretReferenceHashPrefix);

        Assert.DoesNotContain(scenario.Credential.SecretReference, json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("vault://plantprocess-iq", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("connector_user", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("c************r", json, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [MemberData(nameof(CertifiedConnectors))]
    public void T051_Bad_Credential_Fails_Before_Save_And_Rolls_Back_Staging(string providerType)
    {
        var scenario = ConnectorCertificationScenarios.BadCredentialScenario(providerType);

        var result = Service.Certify(scenario);

        Assert.False(result.Passed);
        Assert.Equal("FailedRolledBack", result.Batch.Status);
        Assert.Equal("bad-credential", result.Batch.ErrorCode);
        Assert.Equal(0, result.Batch.RowCount);
        Assert.Equal(0, result.Staging.CommittedRowCount);
        Assert.Contains("test-before-save", result.FailureReason, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [MemberData(nameof(CertifiedConnectors))]
    public void T051_Malformed_Input_Fails_Import_And_Rolls_Back_Entire_Batch(string providerType)
    {
        var scenario = ConnectorCertificationScenarios.MalformedInputScenario(providerType);

        var result = Service.Certify(scenario);

        Assert.False(result.Passed);
        Assert.Equal("FailedRolledBack", result.Batch.Status);
        Assert.Equal("malformed-source-row", result.Batch.ErrorCode);
        Assert.Equal(0, result.Batch.RowCount);
        Assert.Equal(0, result.Staging.CommittedRowCount);
        Assert.Contains("missing source column", result.FailureReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void T051_Certification_Matrix_Covers_Csv_Excel_MsSql_And_MySql()
    {
        var connectors = ConnectorCertificationScenarios.Phase10ConnectorSet;

        Assert.Contains("Csv", connectors);
        Assert.Contains("Excel", connectors);
        Assert.Contains("SqlServer", connectors);
        Assert.Contains("MySql", connectors);
        Assert.Equal(4, connectors.Count);
    }

    [Fact]
    public void T051_Source_Shaped_Staging_Does_Not_Convert_To_Canonical_Inside_Connector()
    {
        var scenario = ConnectorCertificationScenarios.ValidScenario("SqlServer");

        var result = Service.Certify(scenario);

        Assert.True(result.Passed, result.FailureReason);
        Assert.Contains("finish_temp_c", result.Staging.SourceColumns);
        Assert.DoesNotContain("ParameterObservation", result.Staging.SourceColumns);
        Assert.DoesNotContain("QualityEvent", result.Staging.SourceColumns);
        Assert.DoesNotContain("MaterialUnit", result.Staging.SourceColumns);
    }
}