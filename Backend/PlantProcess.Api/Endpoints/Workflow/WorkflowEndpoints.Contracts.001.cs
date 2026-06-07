using PlantProcess.Application.Analytics.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using PlantProcess.Api.Extensions;
using PlantProcess.Application.Dashboarding.Contracts;
using PlantProcess.Application.Analytics.Interfaces;
using PlantProcess.Application.Contracts.Common;
using PlantProcess.Application.Contracts.DataQuality;
using PlantProcess.Application.Contracts.Materials;
using PlantProcess.Application.Contracts.Process;
using PlantProcess.Application.Contracts.Quality;
using PlantProcess.Application.Integration.Contracts.Commands;
using PlantProcess.Application.Integration.Contracts.Mapping;
using PlantProcess.Application.Integration.Contracts.SourceSystems;
using PlantProcess.Application.Integration.Interfaces.Import;
using PlantProcess.Application.Integration.Interfaces.Mapping;
using PlantProcess.Application.Integration.Interfaces.SourceSystems;
using PlantProcess.Application.Services.DataQuality;
using PlantProcess.Application.Services.Materials;
using PlantProcess.Application.Services.Process;
using PlantProcess.Application.Services.Quality;
using PlantProcess.Infrastructure.Persistence;

using PlantProcess.Api.ErrorHandling;

namespace PlantProcess.Api.Endpoints.Workflow;

// PPIQ_REALIZATION_T027_WORKFLOW_ENDPOINTS_CONTRACTS_SPLIT
public static partial class WorkflowEndpoints
{
public sealed record RegisterSourceSystemRequest(
        string SourceSystemCode,
        string SourceSystemName,
        string SourceSystemType,
        string? Description,
        bool IsReadOnlySource,
        bool IsSynthetic,
        string? SourceSystem,
        string? SourceRecordId);

public sealed record CreateImportBatchRequest(
        Guid SourceSystemDefinitionId,
        string ImportBatchCode,
        string ImportType,
        string? SourceObjectName,
        string? FileName,
        string? Checksum,
        bool IsSynthetic,
        string? SourceSystem,
        string? SourceRecordId);

public sealed record CreateMappingDefinitionRequest(
        Guid SourceSystemDefinitionId,
        string MappingCode,
        string MappingName,
        string SourceObjectName,
        string TargetEntityName,
        string MappingJson,
        string? MappingVersion,
        string? Description,
        bool IsSynthetic,
        string? SourceSystem,
        string? SourceRecordId);

public sealed record CreateWorkflowMaterialRequest(
        string MaterialCode,
        string MaterialUnitType,
        Guid SiteId,
        string? ProductFamily,
        string? GradeOrRecipe,
        DateTime? ProductionStartUtc,
        DateTime? ProductionEndUtc,
        string? PlantTimeZoneId,
        int? PlantUtcOffsetMinutes,
        bool IsSynthetic,
        string? SourceSystem,
        string? SourceRecordId);

public sealed record AddWorkflowMaterialAliasRequest(
        string AliasCode,
        string? AliasType,
        string SourceSystem,
        bool IsSynthetic,
        string? SourceRecordId);

public sealed record CreateWorkflowGenealogyEdgeRequest(
        Guid ParentMaterialUnitId,
        Guid ChildMaterialUnitId,
        string RelationshipType,
        DateTime? EffectiveFromUtc,
        DateTime? EffectiveToUtc,
        decimal? Quantity,
        string? UnitOfMeasure,
        bool IsSynthetic,
        string? SourceSystem,
        string? SourceRecordId);

public sealed record AddWorkflowProcessStepRequest(
        Guid MaterialUnitId,
        Guid? EquipmentId,
        Guid? OperationDefinitionId,
        string OperationType,
        string? OperationCode,
        string? CrewCode,
        DateTime StartedAtUtc,
        DateTime? EndedAtUtc,
        string? ExecutionStatus,
        bool IsSynthetic,
        string? SourceSystem,
        string? SourceRecordId,
        string? PlantTimeZoneId,
        int? PlantUtcOffsetMinutes);

public sealed record AddWorkflowParameterDefinitionRequest(
        string ParameterCode,
        string ParameterName,
        string ValueType,
        string? UnitOfMeasure,
        string? ParameterCategory,
        string? IndustryTemplate,
        decimal? ExpectedMinValue,
        decimal? ExpectedMaxValue,
        bool IsSynthetic,
        string? SourceSystem,
        string? SourceRecordId);

public sealed record AddWorkflowParameterObservationRequest(
        Guid MaterialUnitId,
        Guid? ProcessStepExecutionId,
        Guid ParameterDefinitionId,
        Guid? EquipmentId,
        DateTime ObservedAtUtc,
        decimal? NumericValue,
        string? TextValue,
        bool? BooleanValue,
        string? UnitOfMeasure,
        string? QualityFlag,
        string? RawValue,
        bool IsSynthetic,
        string? SourceSystem,
        string? SourceRecordId,
        string? PlantTimeZoneId,
        int? PlantUtcOffsetMinutes);

public sealed record AddWorkflowProcessEventRequest(
        Guid? MaterialUnitId,
        Guid? ProcessStepExecutionId,
        Guid? EquipmentId,
        string EventType,
        DateTime EventAtUtc,
        string? EventValue,
        string? Description,
        bool IsSynthetic,
        string? SourceSystem,
        string? SourceRecordId,
        string? PlantTimeZoneId,
        int? PlantUtcOffsetMinutes);

public sealed record AddWorkflowDowntimeEventRequest(
        Guid? MaterialUnitId,
        Guid? ProcessStepExecutionId,
        Guid? EquipmentId,
        DateTime StartedAtUtc,
        DateTime? EndedAtUtc,
        string DowntimeType,
        string? ReasonCode,
        string? Description,
        bool IsSynthetic,
        string? SourceSystem,
        string? SourceRecordId,
        string? PlantTimeZoneId,
        int? PlantUtcOffsetMinutes);

public sealed record AddWorkflowDefectCatalogRequest(
        string DefectCode,
        string DefectName,
        string? DefectCategory,
        string? IndustryTemplate,
        bool IsSynthetic,
        string? SourceSystem,
        string? SourceRecordId);

public sealed record AddWorkflowQualityEventRequest(
        Guid MaterialUnitId,
        Guid? DefectCatalogId,
        string EventType,
        DateTime EventAtUtc,
        string? Severity,
        string? Decision,
        string? Description,
        bool IsSynthetic,
        string? SourceSystem,
        string? SourceRecordId,
        string? PlantTimeZoneId,
        int? PlantUtcOffsetMinutes);

public sealed record RaiseWorkflowDataQualityIssueRequest(
        Guid? MaterialUnitId,
        string IssueType,
        string? Severity,
        string Description,
        string? AffectedEntityName,
        Guid? AffectedEntityId,
        bool IsSynthetic,
        string? SourceSystem,
        string? SourceRecordId);

public sealed record StoreWorkflowRiskScoreRequest(
        Guid MaterialUnitId,
        string RiskType,
        decimal Score,
        string? RiskClass,
        string? MainContributorsJson,
        string? ModelVersion,
        bool IsSynthetic,
        string? SourceSystem,
        string? SourceRecordId,
        string? PlantTimeZoneId,
        int? PlantUtcOffsetMinutes);
}
