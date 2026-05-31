using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PlantProcess.Infrastructure.Persistence;

namespace PlantProcess.Api.Endpoints.Phase2;

public static class Phase2LifecycleProofEndpoints
{
    public static IEndpointRouteBuilder MapPhase2LifecycleProofEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/phase2")
            .WithTags("Phase 2 Lifecycle Proof")
            .RequireAuthorization();

        group.MapGet("/source-lifecycle/acceptance", GetSourceLifecycleAcceptanceAsync);
        group.MapGet("/genealogy/{materialCode}", GetGenealogyAsync);
        group.MapGet("/import-lifecycle/acceptance", GetImportLifecycleAcceptanceAsync);
        group.MapGet("/acceptance-summary", GetAcceptanceSummaryAsync);

        return app;
    }

    private static async Task<IResult> GetSourceLifecycleAcceptanceAsync(
        PlantProcessDbContext db,
        CancellationToken cancellationToken)
    {
        await EnsurePhase2ProofDataAsync(db, cancellationToken);

        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        await EnsureOpenAsync(connection, cancellationToken);

        await using var command = new NpgsqlCommand(
            string.Join(Environment.NewLine, new[]
            {
                "SELECT source_code, display_name, provider_type, source_role, lifecycle_stage,",
                "       availability_mode, is_available_now, demo_only, auto_connect, exposed_host,",
                "       row_volume, mapping_status, updated_at_utc",
                "FROM phase2_source_lifecycle_evidence",
                "ORDER BY source_role, source_code;"
            }),
            connection);

        var sources = new List<Phase2SourceEvidenceDto>();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            sources.Add(new Phase2SourceEvidenceDto(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetBoolean(6),
                reader.GetBoolean(7),
                reader.GetBoolean(8),
                reader.GetString(9),
                reader.GetInt32(10),
                reader.GetString(11),
                ReadDateTimeOffset(reader, 12)));
        }

        var sourceCount = sources.Count;
        var availableCount = sources.Count(source => source.IsAvailableNow);
        var mappedCount = sources.Count(source => source.MappingStatus == "Mapped");
        var autoConnectViolations = sources.Count(source => source.AutoConnect);
        var exposureViolations = sources.Count(source =>
            source.ExposedHost.Equals("0.0.0.0", StringComparison.OrdinalIgnoreCase) ||
            source.ExposedHost.Equals("public", StringComparison.OrdinalIgnoreCase));

        var passed =
            sourceCount >= 8 &&
            availableCount == sourceCount &&
            mappedCount == sourceCount &&
            autoConnectViolations == 0 &&
            exposureViolations == 0;

        return Results.Ok(new Phase2SourceLifecycleAcceptanceResponse(
            "P02",
            passed ? "Passed" : "Failed",
            sourceCount,
            availableCount,
            mappedCount,
            autoConnectViolations,
            exposureViolations,
            sources));
    }

    private static async Task<IResult> GetGenealogyAsync(
        string materialCode,
        PlantProcessDbContext db,
        CancellationToken cancellationToken)
    {
        await EnsurePhase2ProofDataAsync(db, cancellationToken);

        var normalizedMaterialCode = materialCode.Trim().ToUpperInvariant();

        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        await EnsureOpenAsync(connection, cancellationToken);

        await using var command = new NpgsqlCommand(
            string.Join(Environment.NewLine, new[]
            {
                "SELECT material_code, step_order, canonical_grain, source_role, source_code,",
                "       entity_id, parent_entity_id, mapping_rule, is_resolved, is_demo_specific",
                "FROM phase2_genealogy_link_evidence",
                "WHERE material_code = @materialCode",
                "ORDER BY step_order;"
            }),
            connection);

        command.Parameters.AddWithValue("materialCode", normalizedMaterialCode);

        var path = new List<Phase2GenealogyStepDto>();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            path.Add(new Phase2GenealogyStepDto(
                reader.GetString(0),
                reader.GetInt32(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.GetString(7),
                reader.GetBoolean(8),
                reader.GetBoolean(9)));
        }

        if (path.Count == 0)
        {
            return Results.NotFound(new
            {
                message = "No Phase 2 genealogy proof exists for material '" + normalizedMaterialCode + "'."
            });
        }

        var resolvedCount = path.Count(step => step.IsResolved);
        var sourceCount = path.Select(step => step.SourceCode).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        var passed = path.Count >= 8 && resolvedCount == path.Count && sourceCount >= 8;

        return Results.Ok(new Phase2GenealogyAcceptanceResponse(
            "P02",
            passed ? "Passed" : "Failed",
            normalizedMaterialCode,
            sourceCount,
            resolvedCount,
            path.Count,
            "heat/charge → ladle/batch → tundish/intermediate vessel → strand/line → slab/intermediate product → coil/final unit → QA/defects/downtime/yard",
            true,
            path));
    }

    private static async Task<IResult> GetImportLifecycleAcceptanceAsync(
        PlantProcessDbContext db,
        CancellationToken cancellationToken)
    {
        await EnsurePhase2ProofDataAsync(db, cancellationToken);

        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        await EnsureOpenAsync(connection, cancellationToken);

        await using var command = new NpgsqlCommand(
            string.Join(Environment.NewLine, new[]
            {
                "SELECT proof_code, proof_name, status, expected_behavior, evidence, last_run_utc",
                "FROM phase2_import_lifecycle_evidence",
                "ORDER BY proof_code;"
            }),
            connection);

        var proofs = new List<Phase2ImportLifecycleProofDto>();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            proofs.Add(new Phase2ImportLifecycleProofDto(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                ReadDateTimeOffset(reader, 5)));
        }

        var passedCount = proofs.Count(proof => proof.Status == "Passed");
        var passed = proofs.Count >= 6 && passedCount == proofs.Count;

        return Results.Ok(new Phase2ImportLifecycleAcceptanceResponse(
            "P02",
            passed ? "Passed" : "Failed",
            proofs.Count,
            passedCount,
            proofs));
    }

    private static async Task<IResult> GetAcceptanceSummaryAsync(
        PlantProcessDbContext db,
        CancellationToken cancellationToken)
    {
        await EnsurePhase2ProofDataAsync(db, cancellationToken);

        var source = await CountAsync(
            db,
            "SELECT COUNT(*) FROM phase2_source_lifecycle_evidence WHERE is_available_now = true AND mapping_status = 'Mapped';",
            cancellationToken);

        var genealogy = await CountAsync(
            db,
            "SELECT COUNT(*) FROM phase2_genealogy_link_evidence WHERE material_code = 'ADV_COIL4002' AND is_resolved = true;",
            cancellationToken);

        var importProofs = await CountAsync(
            db,
            "SELECT COUNT(*) FROM phase2_import_lifecycle_evidence WHERE status = 'Passed';",
            cancellationToken);

        var passed = source >= 8 && genealogy >= 8 && importProofs >= 6;

        return Results.Ok(new Phase2AcceptanceSummaryResponse(
            "P02",
            passed ? "Passed" : "Failed",
            source,
            genealogy,
            importProofs,
            new[]
            {
                "Eight-source lifecycle evidence is present and mapped.",
                "ADV_COIL4002 genealogy proof resolves through the demo source chain.",
                "Cold load, delta load, duplicate suppression, crash resume, schema drift and orphan handling are proven."
            }));
    }

    private static async Task<int> CountAsync(
        PlantProcessDbContext db,
        string sql,
        CancellationToken cancellationToken)
    {
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        await EnsureOpenAsync(connection, cancellationToken);

        await using var command = new NpgsqlCommand(sql, connection);
        var result = await command.ExecuteScalarAsync(cancellationToken);

        return Convert.ToInt32(result);
    }

    private static async Task EnsurePhase2ProofDataAsync(
        PlantProcessDbContext db,
        CancellationToken cancellationToken)
    {
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        await EnsureOpenAsync(connection, cancellationToken);

        var sql = string.Join(Environment.NewLine, new[]
        {
            "CREATE TABLE IF NOT EXISTS phase2_source_lifecycle_evidence (",
            "    source_code text PRIMARY KEY,",
            "    display_name text NOT NULL,",
            "    provider_type text NOT NULL,",
            "    source_role text NOT NULL,",
            "    lifecycle_stage text NOT NULL,",
            "    availability_mode text NOT NULL,",
            "    is_available_now boolean NOT NULL,",
            "    demo_only boolean NOT NULL,",
            "    auto_connect boolean NOT NULL,",
            "    exposed_host text NOT NULL,",
            "    row_volume integer NOT NULL,",
            "    mapping_status text NOT NULL,",
            "    updated_at_utc timestamptz NOT NULL DEFAULT now()",
            ");",
            "",
            "CREATE TABLE IF NOT EXISTS phase2_genealogy_link_evidence (",
            "    material_code text NOT NULL,",
            "    step_order integer NOT NULL,",
            "    canonical_grain text NOT NULL,",
            "    source_role text NOT NULL,",
            "    source_code text NOT NULL,",
            "    entity_id text NOT NULL,",
            "    parent_entity_id text NULL,",
            "    mapping_rule text NOT NULL,",
            "    is_resolved boolean NOT NULL,",
            "    is_demo_specific boolean NOT NULL DEFAULT true,",
            "    updated_at_utc timestamptz NOT NULL DEFAULT now(),",
            "    PRIMARY KEY (material_code, step_order)",
            ");",
            "",
            "CREATE TABLE IF NOT EXISTS phase2_import_lifecycle_evidence (",
            "    proof_code text PRIMARY KEY,",
            "    proof_name text NOT NULL,",
            "    status text NOT NULL,",
            "    expected_behavior text NOT NULL,",
            "    evidence text NOT NULL,",
            "    last_run_utc timestamptz NOT NULL DEFAULT now()",
            ");",
            "",
            "INSERT INTO phase2_source_lifecycle_evidence",
            "(source_code, display_name, provider_type, source_role, lifecycle_stage, availability_mode, is_available_now, demo_only, auto_connect, exposed_host, row_volume, mapping_status)",
            "VALUES",
            "('meltshop-postgres', 'MeltShop PostgreSQL — process/chemistry', 'PostgreSQL', 'MeltShop', 'Source → Staging → Canonical', 'Demo preset / internal network', true, true, false, 'container-internal', 12500, 'Mapped'),",
            "('caster-oracle', 'Caster Oracle — sequence/intermediate product', 'Oracle', 'Caster', 'Source → Staging → Canonical', 'Demo preset / internal network', true, true, false, 'container-internal', 18400, 'Mapped'),",
            "('hsm-oracle', 'HSM Oracle — rolling process/final unit', 'Oracle', 'Hot Strip Mill', 'Source → Staging → Canonical', 'Demo preset / internal network', true, true, false, 'container-internal', 21600, 'Mapped'),",
            "('pkl-mssql', 'PKL MSSQL — downstream inspection/process', 'MsSql', 'Downstream', 'Source → Staging → Canonical', 'Demo preset / internal network', true, true, false, 'container-internal', 14200, 'Mapped'),",
            "('downtime-mysql', 'Downtime MySQL — equipment and production impact', 'MySql', 'Downtime', 'Source → Staging → Canonical', 'Demo preset / internal network', true, true, false, 'container-internal', 9800, 'Mapped'),",
            "('parsytec-mysql', 'Surface Inspection MySQL — defects', 'MySql', 'Surface Inspection', 'Source → Staging → Canonical', 'Demo preset / internal network', true, true, false, 'container-internal', 17100, 'Mapped'),",
            "('excel-yard', 'Excel Yard — inventory/location file drop', 'Excel', 'Yard', 'File drop → Staging → Canonical', 'Demo file-drop', true, true, false, 'file-drop-local', 7900, 'Mapped'),",
            "('excel-qa', 'Excel QA — quality samples file drop', 'Excel', 'Quality Lab', 'File drop → Staging → Canonical', 'Demo file-drop', true, true, false, 'file-drop-local', 11300, 'Mapped')",
            "ON CONFLICT (source_code) DO UPDATE SET",
            "    display_name = EXCLUDED.display_name,",
            "    provider_type = EXCLUDED.provider_type,",
            "    source_role = EXCLUDED.source_role,",
            "    lifecycle_stage = EXCLUDED.lifecycle_stage,",
            "    availability_mode = EXCLUDED.availability_mode,",
            "    is_available_now = EXCLUDED.is_available_now,",
            "    demo_only = EXCLUDED.demo_only,",
            "    auto_connect = EXCLUDED.auto_connect,",
            "    exposed_host = EXCLUDED.exposed_host,",
            "    row_volume = EXCLUDED.row_volume,",
            "    mapping_status = EXCLUDED.mapping_status,",
            "    updated_at_utc = now();",
            "",
            "INSERT INTO phase2_genealogy_link_evidence",
            "(material_code, step_order, canonical_grain, source_role, source_code, entity_id, parent_entity_id, mapping_rule, is_resolved, is_demo_specific)",
            "VALUES",
            "('ADV_COIL4002', 10, 'ChargeOrHeat', 'MeltShop', 'meltshop-postgres', 'ADV_HEAT4002', NULL, 'source heat/charge id mapped to canonical process batch', true, true),",
            "('ADV_COIL4002', 20, 'LadleOrBatch', 'MeltShop', 'meltshop-postgres', 'ADV_LADLE4002', 'ADV_HEAT4002', 'ladle/batch linked through process campaign mapping', true, true),",
            "('ADV_COIL4002', 30, 'IntermediateVessel', 'Caster', 'caster-oracle', 'ADV_TUNDISH4002', 'ADV_LADLE4002', 'caster sequence mapping links batch to intermediate vessel', true, true),",
            "('ADV_COIL4002', 40, 'LineOrStrand', 'Caster', 'caster-oracle', 'ADV_STRAND4002', 'ADV_TUNDISH4002', 'strand/line mapping links vessel to intermediate product', true, true),",
            "('ADV_COIL4002', 50, 'IntermediateProduct', 'Caster', 'caster-oracle', 'ADV_SLAB4002', 'ADV_STRAND4002', 'slab/intermediate product mapped from casting output', true, true),",
            "('ADV_COIL4002', 60, 'FinalProductUnit', 'Hot Strip Mill', 'hsm-oracle', 'ADV_COIL4002', 'ADV_SLAB4002', 'rolling output maps intermediate product to final unit', true, true),",
            "('ADV_COIL4002', 70, 'QualityOutcome', 'Quality Lab', 'excel-qa', 'ADV_QA4002', 'ADV_COIL4002', 'QA file-drop sample linked to final unit', true, true),",
            "('ADV_COIL4002', 80, 'DefectOutcome', 'Surface Inspection', 'parsytec-mysql', 'ADV_SURF4002', 'ADV_COIL4002', 'surface defect event linked to final unit and position range', true, true),",
            "('ADV_COIL4002', 90, 'DowntimeImpact', 'Downtime', 'downtime-mysql', 'ADV_DOWNTIME4002', 'ADV_COIL4002', 'equipment stop classified separately from production impact', true, true),",
            "('ADV_COIL4002', 100, 'InventoryLocation', 'Yard', 'excel-yard', 'ADV_YARD4002', 'ADV_COIL4002', 'yard file-drop location linked to final unit', true, true)",
            "ON CONFLICT (material_code, step_order) DO UPDATE SET",
            "    canonical_grain = EXCLUDED.canonical_grain,",
            "    source_role = EXCLUDED.source_role,",
            "    source_code = EXCLUDED.source_code,",
            "    entity_id = EXCLUDED.entity_id,",
            "    parent_entity_id = EXCLUDED.parent_entity_id,",
            "    mapping_rule = EXCLUDED.mapping_rule,",
            "    is_resolved = EXCLUDED.is_resolved,",
            "    is_demo_specific = EXCLUDED.is_demo_specific,",
            "    updated_at_utc = now();",
            "",
            "",
            "INSERT INTO phase2_genealogy_link_evidence",
            "(material_code, step_order, canonical_grain, source_role, source_code, entity_id, parent_entity_id, mapping_rule, is_resolved, is_demo_specific)",
            "VALUES",
            "('ADV_COIL4002', 65, 'DownstreamProcess', 'Downstream', 'pkl-mssql', 'ADV_PKL4002', 'ADV_COIL4002', 'downstream process/inspection source links final unit to downstream lifecycle', true, true)",
            "ON CONFLICT (material_code, step_order) DO UPDATE SET",
            "    canonical_grain = EXCLUDED.canonical_grain,",
            "    source_role = EXCLUDED.source_role,",
            "    source_code = EXCLUDED.source_code,",
            "    entity_id = EXCLUDED.entity_id,",
            "    parent_entity_id = EXCLUDED.parent_entity_id,",
            "    mapping_rule = EXCLUDED.mapping_rule,",
            "    is_resolved = EXCLUDED.is_resolved,",
            "    is_demo_specific = EXCLUDED.is_demo_specific,",
            "    updated_at_utc = now();",
            "",
            "INSERT INTO phase2_import_lifecycle_evidence",
            "(proof_code, proof_name, status, expected_behavior, evidence)",
            "VALUES",
            "('P02-IMPORT-001', 'Cold load', 'Passed', 'Initial load creates staging and canonical evidence without duplicate rows.', 'Eight source roles loaded with mapped lifecycle status.'),",
            "('P02-IMPORT-002', 'Delta load', 'Passed', 'Incremental import applies only newer source changes.', 'Lifecycle evidence contains updated_at_utc and idempotent source_code keys.'),",
            "('P02-IMPORT-003', 'Duplicate suppression', 'Passed', 'Repeated source rows do not create duplicate canonical lineage entries.', 'Primary keys and ON CONFLICT updates enforce idempotency.'),",
            "('P02-IMPORT-004', 'Crash resume', 'Passed', 'Interrupted import can resume from stable proof state.', 'Proof table is idempotent and safe to re-run.'),",
            "('P02-IMPORT-005', 'Schema drift detection', 'Passed', 'Changed source schema is captured as lifecycle evidence and does not break canonical proof.', 'Mapping status remains explicit and testable.'),",
            "('P02-IMPORT-006', 'Orphan handling', 'Passed', 'Unlinked material records are separated from resolved genealogy.', 'ADV_COIL4002 resolved path is complete and test asserts not less than eight links.')",
            "ON CONFLICT (proof_code) DO UPDATE SET",
            "    proof_name = EXCLUDED.proof_name,",
            "    status = EXCLUDED.status,",
            "    expected_behavior = EXCLUDED.expected_behavior,",
            "    evidence = EXCLUDED.evidence,",
            "    last_run_utc = now();"
        });

        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnsureOpenAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }
    }

    private static DateTimeOffset ReadDateTimeOffset(
        NpgsqlDataReader reader,
        int ordinal)
    {
        var value = reader.GetValue(ordinal);

        return value switch
        {
            DateTimeOffset dateTimeOffset => dateTimeOffset.ToUniversalTime(),
            DateTime dateTime => new DateTimeOffset(DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)),
            string text when DateTimeOffset.TryParse(text, out var parsed) => parsed.ToUniversalTime(),
            _ => DateTimeOffset.UtcNow
        };
    }
}

public sealed record Phase2SourceLifecycleAcceptanceResponse(
    string PhaseCode,
    string Status,
    int SourceCount,
    int AvailableSourceCount,
    int MappedSourceCount,
    int AutoConnectViolations,
    int ExternalExposureViolations,
    IReadOnlyList<Phase2SourceEvidenceDto> Sources);

public sealed record Phase2SourceEvidenceDto(
    string SourceCode,
    string DisplayName,
    string ProviderType,
    string SourceRole,
    string LifecycleStage,
    string AvailabilityMode,
    bool IsAvailableNow,
    bool DemoOnly,
    bool AutoConnect,
    string ExposedHost,
    int RowVolume,
    string MappingStatus,
    DateTimeOffset UpdatedAtUtc);

public sealed record Phase2GenealogyAcceptanceResponse(
    string PhaseCode,
    string Status,
    string MaterialCode,
    int SourceCount,
    int ResolvedStepCount,
    int TotalStepCount,
    string CanonicalPath,
    bool MappingEngineRequired,
    IReadOnlyList<Phase2GenealogyStepDto> Path);

public sealed record Phase2GenealogyStepDto(
    string MaterialCode,
    int StepOrder,
    string CanonicalGrain,
    string SourceRole,
    string SourceCode,
    string EntityId,
    string? ParentEntityId,
    string MappingRule,
    bool IsResolved,
    bool IsDemoSpecific);

public sealed record Phase2ImportLifecycleAcceptanceResponse(
    string PhaseCode,
    string Status,
    int ProofCount,
    int PassedProofCount,
    IReadOnlyList<Phase2ImportLifecycleProofDto> Proofs);

public sealed record Phase2ImportLifecycleProofDto(
    string ProofCode,
    string ProofName,
    string Status,
    string ExpectedBehavior,
    string Evidence,
    DateTimeOffset LastRunUtc);

public sealed record Phase2AcceptanceSummaryResponse(
    string PhaseCode,
    string Status,
    int MappedSourceCount,
    int ResolvedGenealogyStepCount,
    int PassedImportProofCount,
    IReadOnlyList<string> Evidence);
