// ============================================================
// File: Backend/PlantProcess.Application/Integration/Security/SafeSqlValidator.cs
// Task: BE-FIX-002 — relocated from Api → Application for testability
//
// Note: the original earlier version of this file included a helper
// ApplyExecutionSafetyAsync(NpgsqlConnection) which has been REMOVED
// from this Application-layer copy because Application cannot reference
// Npgsql. Apply the same SET LOCAL statement_timeout statement inline
// in the endpoint that runs the preview, OR add a small helper in
// Infrastructure if you prefer.
// ============================================================

using System.Text.RegularExpressions;
using PlantProcess.Application.Integration.Contracts.Dtos;

namespace PlantProcess.Application.Integration.Security;

public static class SafeSqlValidator
{
    private static readonly string[] ForbiddenTokens =
    {
        "insert", "update", "delete", "drop", "alter", "truncate",
        "create", "grant", "revoke", "execute", "exec", "merge",
        "copy", "vacuum", "analyze", "call", "do", "listen",
        "notify", "unlisten", "set", "reset", "prepare", "deallocate",

        "pg_read_file", "pg_read_binary_file", "pg_ls_dir",
        "pg_stat_file", "pg_read_server_files",
        "lo_import", "lo_export", "lo_create", "lo_unlink",
        "dblink", "dblink_exec", "dblink_send_query",
        "dblink_connect", "dblink_disconnect",
        "pg_sleep", "pg_sleep_for", "pg_sleep_until",
        "pg_logfile_rotate", "pg_reload_conf",

        "pg_catalog", "information_schema", "pg_proc", "pg_authid",

        "xp_cmdshell", "xp_dirtree", "xp_fileexist", "xp_subdirs",
        "xp_regread", "xp_regwrite",
        "openrowset", "opendatasource", "openquery",
        "bulk insert", "waitfor",
        "sp_executesql", "sp_oacreate", "sp_oamethod",
    };

    private static readonly HashSet<string> AllowedTables = new(StringComparer.OrdinalIgnoreCase)
    {
        "staging_records",
        "sites", "areas", "equipment",
        "material_units", "material_aliases", "genealogy_edges",
        "process_step_executions", "parameter_definitions",
        "parameter_observations", "process_events", "downtime_events",
        "defect_catalog", "quality_events", "data_quality_issues",
        "risk_scores", "correlation_results",
        "kpi_definitions", "schema_view_definitions",
        "job_definitions", "job_run_history",
        "vw_defect_by_shift", "vw_material_with_defects",
        "vw_daily_quality_summary", "vw_correlation_input",
    };

    private static HashSet<string> ExtractCteNames(string sql)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Match the first CTE: WITH [RECURSIVE] name AS (
        var first = Regex.Match(sql,
            @"\bWITH\s+(?:RECURSIVE\s+)?(\w+)\s+AS\s*\(",
            RegexOptions.IgnoreCase);
        if (!first.Success) return names;

        names.Add(first.Groups[1].Value);

        // Match any additional CTEs in the same WITH clause: , name AS (
        foreach (Match m in Regex.Matches(sql,
            @",\s*(\w+)\s+AS\s*\(",
            RegexOptions.IgnoreCase))
        {
            names.Add(m.Groups[1].Value);
        }

        return names;
    }

    private static readonly Regex FromOrJoinPattern = new(
        @"\b(?:from|(?:cross\s+|natural\s+|inner\s+|left\s+(?:outer\s+)?|right\s+(?:outer\s+)?|full\s+(?:outer\s+)?)?join)\s+(?:""([^""]+)""|`([^`]+)`|([a-zA-Z_][a-zA-Z0-9_\.]*))",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex CommaJoinPattern = new(
        @"(?:from|join)\s+[a-zA-Z_][\w\.]*(?:\s+(?:as\s+)?\w+)?\s*((?:,\s*[a-zA-Z_][\w\.]*\s*)+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex IdentifierPattern = new(
        @"[a-zA-Z_][\w\.]*",
        RegexOptions.CultureInvariant);

    private static readonly Regex BlockCommentPattern = new(
        @"/\*[\s\S]*?\*/",
        RegexOptions.CultureInvariant);

    private static readonly Regex LineCommentPattern = new(
        @"--[^\r\n]*",
        RegexOptions.CultureInvariant);


    public static SqlSafetyValidationResultDto Validate(string? sqlText)
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        var referencedTables = new List<string>();

        if (string.IsNullOrWhiteSpace(sqlText))
        {
            errors.Add("SQL text is required.");
            return new SqlSafetyValidationResultDto(false, errors, warnings, referencedTables);
        }

        var sql = StripSqlComments(sqlText).Trim();

        var trimmed = sql.TrimEnd(';', ' ', '\t', '\r', '\n');
        if (trimmed.Contains(';'))
            errors.Add("Multiple statements are not allowed. Submit one SELECT or WITH query only.");

        if (!trimmed.StartsWith("select", StringComparison.OrdinalIgnoreCase) &&
            !trimmed.StartsWith("with",   StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("Only SELECT or WITH queries are allowed.");
        }

        // Extract CTE names AFTER comments are stripped — these are scoped
        // virtual tables and should be auto-allowed for this query.
        var cteNames = ExtractCteNames(trimmed);

        var lowered = Regex.Replace(trimmed.ToLowerInvariant(), @"\s+", " ");

        foreach (var token in ForbiddenTokens)
        {
            var pattern = $@"(\b{Regex.Escape(token)}\b|\b{Regex.Escape(token)}\s*\()";
            if (Regex.IsMatch(lowered, pattern, RegexOptions.IgnoreCase))
                errors.Add($"Forbidden SQL token detected: {token}");
        }

        foreach (Match match in FromOrJoinPattern.Matches(trimmed))
        {
            var rawTableName =
                !string.IsNullOrEmpty(match.Groups[1].Value) ? match.Groups[1].Value :
                !string.IsNullOrEmpty(match.Groups[2].Value) ? match.Groups[2].Value :
                match.Groups[3].Value;

            var bare = StripSchemaPrefix(rawTableName);

            // CTE aliases are scoped to this query — always allowed.
            if (cteNames.Contains(bare))
                continue;

            referencedTables.Add(bare);

            if (!AllowedTables.Contains(bare))
                errors.Add($"Table '{rawTableName}' is not in the allowed table list.");
        }

        foreach (Match cj in CommaJoinPattern.Matches(trimmed))
        {
            var commaList = cj.Groups[1].Value;
            foreach (Match id in IdentifierPattern.Matches(commaList))
            {
                var bare = StripSchemaPrefix(id.Value);

                // CTE aliases are scoped to this query — always allowed.
                if (cteNames.Contains(bare))
                    continue;

                referencedTables.Add(bare);
                if (!AllowedTables.Contains(bare))
                    errors.Add($"Table '{id.Value}' is not in the allowed table list.");
            }
        }

        if (Regex.IsMatch(lowered, @"\bwith\s+recursive\b"))
        {
            if (!Regex.IsMatch(lowered, @"\blimit\s+\d+\b"))
                errors.Add("WITH RECURSIVE queries must include an explicit LIMIT clause.");
        }

        if (referencedTables.Count == 0)
            warnings.Add("No FROM/JOIN table reference detected. The query may not be useful.");

        if (!Regex.IsMatch(lowered, @"\blimit\s+\d+\b"))
            warnings.Add("No LIMIT found. The preview endpoint wraps the query and applies its own LIMIT.");

        var distinctTables = referencedTables
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new SqlSafetyValidationResultDto(
            IsValid:          errors.Count == 0,
            Errors:           errors,
            Warnings:         warnings,
            ReferencedTables: distinctTables);
    }

       private static string StripSqlComments(string sql)
    {
        var withoutBlocks = BlockCommentPattern.Replace(sql, " ");
        var withoutLines  = LineCommentPattern.Replace(withoutBlocks, " ");
        return withoutLines;
    }

    private static string StripSchemaPrefix(string identifier)
    {
        return identifier.Contains('.')
            ? identifier.Split('.').Last()
            : identifier;
    }
}