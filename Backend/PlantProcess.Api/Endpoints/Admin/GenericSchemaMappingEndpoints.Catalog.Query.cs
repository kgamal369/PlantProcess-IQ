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

// PPIQ_REALIZATION_T026_GENERIC_SCHEMA_MAPPING_CATALOG_QUERY_SPLIT
public static partial class GenericSchemaMappingEndpoints
{
private static async Task<IResult> GetCatalogAsync(
        PlantProcessDbContext db,
        CancellationToken cancellationToken)
    {
        await EnsureCatalogAsync(db, cancellationToken);

        const string sql = """
        SELECT
            id,
            view_code,
            view_name,
            view_kind,
            target_entity,
            physical_schema,
            physical_view_name,
            sql_text,
            output_schema_json::text,
            mapping_json::text,
            source_dataset_ids::text,
            attached_scope_type,
            attached_scope_code,
            is_registered,
            is_approved,
            is_active,
            is_system_seed,
            last_validated_at_utc,
            last_validation_status,
            last_validation_message,
            last_executed_at_utc,
            last_execution_status,
            last_execution_message,
            last_execution_row_count,
            created_by,
            created_at_utc,
            updated_at_utc
        FROM ppiq_meta.canonical_schema_views
        WHERE is_deleted = false
        ORDER BY
            CASE view_kind
                WHEN 'MappingPreparationView' THEN 1
                WHEN 'JoinView' THEN 2
                WHEN 'KpiView' THEN 3
                ELSE 9
            END,
            view_code;
        """;

        var rows = await QueryAsync(db, sql, cancellationToken);
        return Results.Ok(rows);
    }

private static async Task EnsureCatalogAsync(
        PlantProcessDbContext db,
        CancellationToken cancellationToken)
    {
        await ExecuteNonQueryAsync(
            db,
            """
            CREATE EXTENSION IF NOT EXISTS pgcrypto;

            CREATE TABLE IF NOT EXISTS ppiq_meta.canonical_schema_views
            (
                id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                view_code text NOT NULL,
                view_name text NOT NULL,
                view_kind text NOT NULL,
                target_entity text NOT NULL,
                physical_schema text NOT NULL DEFAULT 'public',
                physical_view_name text NOT NULL,
                sql_text text NOT NULL,
                output_schema_json jsonb NOT NULL DEFAULT '[]'::jsonb,
                mapping_json jsonb NOT NULL DEFAULT '{}'::jsonb,
                source_dataset_ids jsonb NOT NULL DEFAULT '[]'::jsonb,
                attached_scope_type text NULL,
                attached_scope_code text NULL,
                is_registered boolean NOT NULL DEFAULT true,
                is_approved boolean NOT NULL DEFAULT false,
                is_active boolean NOT NULL DEFAULT true,
                is_system_seed boolean NOT NULL DEFAULT false,
                last_validated_at_utc timestamptz NULL,
                last_validation_status text NULL,
                last_validation_message text NULL,
                last_executed_at_utc timestamptz NULL,
                last_execution_status text NULL,
                last_execution_message text NULL,
                last_execution_row_count integer NULL,
                created_by text NULL,
                created_at_utc timestamptz NOT NULL DEFAULT now(),
                updated_at_utc timestamptz NULL,
                is_deleted boolean NOT NULL DEFAULT false,
                deleted_at_utc timestamptz NULL,
                deleted_reason text NULL
            );

            CREATE UNIQUE INDEX IF NOT EXISTS ux_canonical_schema_views_view_code_active
            ON ppiq_meta.canonical_schema_views (lower(view_code))
            WHERE is_deleted = false;

            CREATE TABLE IF NOT EXISTS ppiq_meta.schema_mapping_executions
            (
                id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                canonical_schema_view_id uuid NULL,
                view_code text NOT NULL,
                target_entity text NOT NULL,
                execution_mode text NOT NULL DEFAULT 'ValidateAndRefreshView',
                status text NOT NULL,
                message text NULL,
                row_count integer NOT NULL DEFAULT 0,
                duration_ms integer NOT NULL DEFAULT 0,
                executed_by text NULL,
                started_at_utc timestamptz NOT NULL DEFAULT now(),
                completed_at_utc timestamptz NULL,
                details_json jsonb NOT NULL DEFAULT '{}'::jsonb
            );

            CREATE TABLE IF NOT EXISTS ppiq_meta.canonical_schema_view_audit
            (
                id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                canonical_schema_view_id uuid NULL,
                action_code text NOT NULL,
                action_status text NOT NULL,
                action_message text NULL,
                payload_json jsonb NOT NULL DEFAULT '{}'::jsonb,
                executed_by text NULL,
                executed_at_utc timestamptz NOT NULL DEFAULT now()
            );
            """,
            cancellationToken);
    }

private static async Task<IReadOnlyDictionary<string, object?>> GetCatalogByIdAsync(
        PlantProcessDbContext db,
        Guid id,
        CancellationToken cancellationToken)
    {
        var rows = await QueryAsync(
            db,
            """
            SELECT
                id,
                view_code,
                view_name,
                view_kind,
                target_entity,
                physical_schema,
                physical_view_name,
                sql_text,
                output_schema_json::text,
                mapping_json::text,
                source_dataset_ids::text,
                attached_scope_type,
                attached_scope_code,
                is_registered,
                is_approved,
                is_active,
                is_system_seed,
                last_validated_at_utc,
                last_validation_status,
                last_validation_message,
                last_executed_at_utc,
                last_execution_status,
                last_execution_message,
                last_execution_row_count,
                created_by,
                created_at_utc,
                updated_at_utc
            FROM ppiq_meta.canonical_schema_views
            WHERE id = @id;
            """,
            cancellationToken,
            ("id", id));

        return rows.Single();
    }
}
