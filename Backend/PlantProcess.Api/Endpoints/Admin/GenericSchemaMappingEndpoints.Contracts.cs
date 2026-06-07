using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlantProcess.Infrastructure.Persistence;

using PlantProcess.Api.ErrorHandling;

namespace PlantProcess.Api.Endpoints.Admin;

// PPIQ_REALIZATION_T026_GENERIC_SCHEMA_MAPPING_CONTRACTS_SPLIT
public static partial class GenericSchemaMappingEndpoints
{
public sealed record RegisterCanonicalViewRequest(
        string ViewCode,
        string ViewName,
        string ViewKind,
        string TargetEntity,
        string SqlText,
        string? PhysicalSchema,
        string? PhysicalViewName,
        string? OutputSchemaJson,
        string? MappingJson,
        string? SourceDatasetIdsJson,
        string? AttachedScopeType,
        string? AttachedScopeCode,
        bool IsSystemSeed);

    public sealed record ResolveSchemaViewRequest(
        string? ViewCode,
        string? TargetEntity,
        string? WidgetCode,
        string? MeasureCode,
        string? DimensionCode);

    public sealed record JoinColumnSelection(
        string Side,
        string Column,
        string? Alias);

    public sealed record CrossSourceJoinRequest(
        string LeftSchema,
        string LeftTable,
        string RightSchema,
        string RightTable,
        string LeftJoinColumn,
        string RightJoinColumn,
        IReadOnlyList<JoinColumnSelection> Columns,
        int? MaxRows);

    public sealed record MaterializeJoinRequest(
        string ViewCode,
        string ViewName,
        CrossSourceJoinRequest Join,
        string? TargetEntity,
        string? PhysicalSchema,
        string? PhysicalViewName,
        string? MappingJson,
        string? SourceDatasetIdsJson,
        string? AttachedScopeType,
        string? AttachedScopeCode);

    public sealed record KpiViewRequest(
        string ViewCode,
        string ViewName,
        string KpiCode,
        string? KpiName,
        string? KpiCategory,
        string SqlText,
        string? PhysicalSchema,
        string? PhysicalViewName,
        string? Unit,
        string? ValueExpression,
        string? DimensionExpression,
        string? FilterExpression,
        string? AggregationType,
        string? KpiOptionsJson,
        string? MappingJson,
        string? AttachedScopeType,
        string? AttachedScopeCode,
        bool IsSynthetic);

    public sealed record ExecuteMappingRequest(
        string? ExecutionMode,
        bool PreviewOnly,
        bool StopOnFirstError);

    public sealed record PreviewColumn(
        string ColumnName,
        string DataType,
        int Ordinal);
}
