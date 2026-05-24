// ============================================================
// File: Backend/PlantProcess.Application/Integration/Security/SafeSqlValidator.cs
// Task: BE-FIX-002
//
// Purpose:
// - Central, testable SQL safety validator for Schema View Builder.
// - Allows only one SELECT/WITH query.
// - Blocks DML/DDL/admin commands.
// - Blocks PostgreSQL and MSSQL exfiltration / server-side functions.
// - Blocks system schemas.
// - Catches quoted identifiers, backtick identifiers, all JOIN styles,
//   and ANSI comma-joins.
// - Requires LIMIT for WITH RECURSIVE.
// ============================================================

using System.Text.RegularExpressions;
using PlantProcess.Application.Integration.Contracts.Dtos;

namespace PlantProcess.Application.Integration.Security;

public static class SafeSqlValidator
{
    private static readonly string[] ForbiddenTokens =
    {
        // DML / DDL / admin / session control
        "insert",
        "update",
        "delete",
        "drop",
        "alter",
        "truncate",
        "create",
        "grant",
        "revoke",
        "execute",
        "exec",
        "merge",
        "copy",
        "vacuum",
        "analyze",
        "call",
        "do",
        "listen",
        "notify",
        "unlisten",
        "set",
        "reset",
        "prepare",
        "deallocate",

        // PostgreSQL file/server/system functions
        "pg_read_file",
        "pg_read_binary_file",
        "pg_ls_dir",
        "pg_stat_file",
        "pg_read_server_files",
        "pg_logfile_rotate",
        "pg_reload_conf",

        // PostgreSQL large-object filesystem helpers
        "lo_import",
        "lo_export",
        "lo_create",
        "lo_unlink",

        // PostgreSQL external connection helpers
        "dblink",
        "dblink_exec",
        "dblink_send_query",
        "dblink_connect",
        "dblink_disconnect",

        // PostgreSQL delay / resource exhaustion helpers
        "pg_sleep",
        "pg_sleep_for",
        "pg_sleep_until",

        // PostgreSQL system schemas / sensitive catalogs
        "pg_catalog",
        "information_schema",
        "pg_proc",
        "pg_authid",
        "pg_shadow",
        "pg_roles",
        "pg_user",

        // SQL Server dangerous procedures / external rowsets
        "xp_cmdshell",
        "xp_dirtree",
        "xp_fileexist",
        "xp_subdirs",
        "xp_regread",
        "xp_regwrite",
        "sp_executesql",
        "sp_oacreate",
        "sp_oamethod",
        "openrowset",
        "opendatasource",
        "openquery",
        "bulk insert",
        "waitfor"
    };

    private static readonly HashSet<string> SystemSchemas = new(StringComparer.OrdinalIgnoreCase)
    {
        "pg_catalog",
        "information_schema",
        "sys",
        "master",
        "msdb",
        "tempdb"
    };

    private static readonly HashSet<string> AllowedTables = new(StringComparer.OrdinalIgnoreCase)
    {
        // Phase 3 raw / dump / source metadata
        "staging_records",
        "import_batches",
        "source_system_definitions",
        "connection_profiles",
        "source_dataset_definitions",
        "source_field_definitions",

        // Plant layout
        "sites",
        "areas",
        "equipment",

        // Canonical materials / genealogy
        "material_units",
        "material_aliases",
        "genealogy_edges",

        // Process canonical tables
        "process_step_executions",
        "parameter_definitions",
        "parameter_observations",
        "process_events",
        "downtime_events",

        // Quality canonical tables
        "defect_catalogs",
        "defect_catalog",
        "quality_events",
        "data_quality_issues",

        // Analytics
        "risk_scores",
        "correlation_results",
        "model_registry",

        // Schema / KPI / job metadata
        "schema_view_definitions",
        "kpi_definitions",
        "job_definitions",
        "job_run_history",
        "job_run_histories",

        // Approved read models / views
        "vw_defect_by_shift",
        "vw_material_with_defects",
        "vw_daily_quality_summary",
        "vw_correlation_input",
        "mv_dashboard_material_summary",
        "mv_dashboard_quality_daily",
        "mv_dashboard_defect_breakdown"
    };

    private static readonly Regex BlockCommentPattern = new(
        @"/\*[\s\S]*?\*/",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex LineCommentPattern = new(
        @"--[^\r\n]*",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex WhitespacePattern = new(
        @"\s+",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex FromOrJoinPattern = new(
        @"\b(?:from|(?:cross\s+|natural\s+|inner\s+|left\s+(?:outer\s+)?|right\s+(?:outer\s+)?|full\s+(?:outer\s+)?)?join)\s+(?:""([^""]+)""|`([^`]+)`|([a-zA-Z_][a-zA-Z0-9_\.]*))",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex CommaFromPattern = new(
        @"\bfrom\s+(?:""[^""]+""|`[^`]+`|[a-zA-Z_][a-zA-Z0-9_\.]*)(?:\s+(?:as\s+)?[a-zA-Z_][a-zA-Z0-9_]*)?\s*,\s*(?<rest>.+?)(?:\bwhere\b|\bgroup\s+by\b|\border\s+by\b|\bhaving\b|\blimit\b|\boffset\b|$)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex IdentifierPattern = new(
        @"""([^""]+)""|`([^`]+)`|([a-zA-Z_][a-zA-Z0-9_\.]*)",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

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

        var sqlWithoutComments = StripSqlComments(sqlText).Trim();

        if (string.IsNullOrWhiteSpace(sqlWithoutComments))
        {
            errors.Add("SQL text is empty after removing comments.");
            return new SqlSafetyValidationResultDto(false, errors, warnings, referencedTables);
        }

        var sql = RemoveSingleTrailingSemicolon(sqlWithoutComments).Trim();

        if (sql.Contains(';'))
            errors.Add("Multiple SQL statements are not allowed. Submit one SELECT or WITH query only.");

        if (!sql.StartsWith("select", StringComparison.OrdinalIgnoreCase) &&
            !sql.StartsWith("with", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("Only SELECT or WITH queries are allowed.");
        }

        var lowered = WhitespacePattern.Replace(sql.ToLowerInvariant(), " ");

        foreach (var token in ForbiddenTokens)
        {
            if (ContainsForbiddenToken(lowered, token))
                errors.Add($"Forbidden SQL token detected: {token}");
        }

        var cteNames = ExtractCteNames(sql);

        foreach (Match match in FromOrJoinPattern.Matches(sql))
        {
            var rawIdentifier =
                !string.IsNullOrWhiteSpace(match.Groups[1].Value) ? match.Groups[1].Value :
                !string.IsNullOrWhiteSpace(match.Groups[2].Value) ? match.Groups[2].Value :
                match.Groups[3].Value;

            ValidateReferencedTable(rawIdentifier, cteNames, referencedTables, errors);
        }

        foreach (Match commaMatch in CommaFromPattern.Matches(sql))
        {
            var rest = commaMatch.Groups["rest"].Value;

            foreach (Match idMatch in IdentifierPattern.Matches(rest))
            {
                var rawIdentifier =
                    !string.IsNullOrWhiteSpace(idMatch.Groups[1].Value) ? idMatch.Groups[1].Value :
                    !string.IsNullOrWhiteSpace(idMatch.Groups[2].Value) ? idMatch.Groups[2].Value :
                    idMatch.Groups[3].Value;

                if (IsSqlKeyword(rawIdentifier))
                    continue;

                ValidateReferencedTable(rawIdentifier, cteNames, referencedTables, errors);
            }
        }

        if (Regex.IsMatch(lowered, @"\bwith\s+recursive\b", RegexOptions.IgnoreCase) &&
            !Regex.IsMatch(lowered, @"\blimit\s+\d+\b", RegexOptions.IgnoreCase))
        {
            errors.Add("WITH RECURSIVE queries must include an explicit LIMIT clause.");
        }

        if (referencedTables.Count == 0)
            warnings.Add("No FROM/JOIN table reference detected. This is allowed but may not be useful.");

        if (!Regex.IsMatch(lowered, @"\blimit\s+\d+\b", RegexOptions.IgnoreCase))
            warnings.Add("No LIMIT found. The preview endpoint wraps the query and applies its own LIMIT.");

        var distinctTables = referencedTables
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new SqlSafetyValidationResultDto(
            errors.Count == 0,
            errors,
            warnings,
            distinctTables);
    }

    private static bool ContainsForbiddenToken(string loweredSql, string token)
    {
        var escaped = Regex.Escape(token).Replace(@"\ ", @"\s+");

        var pattern = token.Contains(' ', StringComparison.Ordinal)
            ? $@"\b{escaped}\b"
            : $@"\b{escaped}\b\s*(?:\(|\b)";

        return Regex.IsMatch(loweredSql, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static void ValidateReferencedTable(
        string rawIdentifier,
        HashSet<string> cteNames,
        List<string> referencedTables,
        List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(rawIdentifier))
            return;

        var cleaned = UnquoteIdentifier(rawIdentifier.Trim());

        if (IsSqlKeyword(cleaned))
            return;

        var parts = cleaned
            .Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(UnquoteIdentifier)
            .ToArray();

        if (parts.Length == 0)
            return;

        if (parts.Length > 1 && SystemSchemas.Contains(parts[0]))
        {
            errors.Add($"System schema '{parts[0]}' is not allowed.");
            referencedTables.Add(cleaned);
            return;
        }

        var bareTableName = parts[^1];

        if (cteNames.Contains(bareTableName))
            return;

        referencedTables.Add(bareTableName);

        if (!AllowedTables.Contains(bareTableName))
            errors.Add($"Table '{cleaned}' is not in the allowed table list.");
    }

    private static HashSet<string> ExtractCteNames(string sql)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var first = Regex.Match(
            sql,
            @"\bWITH\s+(?:RECURSIVE\s+)?(?:""([^""]+)""|`([^`]+)`|([a-zA-Z_][a-zA-Z0-9_]*))\s+AS\s*\(",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        if (!first.Success)
            return names;

        names.Add(FirstNonEmptyGroup(first.Groups[1].Value, first.Groups[2].Value, first.Groups[3].Value));

        foreach (Match match in Regex.Matches(
            sql,
            @",\s*(?:""([^""]+)""|`([^`]+)`|([a-zA-Z_][a-zA-Z0-9_]*))\s+AS\s*\(",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            names.Add(FirstNonEmptyGroup(match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value));
        }

        return names;
    }

    private static string FirstNonEmptyGroup(params string[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return UnquoteIdentifier(value);
        }

        return string.Empty;
    }

    private static string StripSqlComments(string sql)
    {
        var withoutBlocks = BlockCommentPattern.Replace(sql, " ");
        return LineCommentPattern.Replace(withoutBlocks, " ");
    }

    private static string RemoveSingleTrailingSemicolon(string sql)
    {
        return Regex.Replace(sql, @";\s*$", string.Empty, RegexOptions.CultureInvariant);
    }

    private static string UnquoteIdentifier(string identifier)
    {
        var value = identifier.Trim();

        if (value.Length >= 2 &&
            ((value[0] == '"' && value[^1] == '"') ||
             (value[0] == '`' && value[^1] == '`') ||
             (value[0] == '[' && value[^1] == ']')))
        {
            return value[1..^1];
        }

        return value;
    }

    private static bool IsSqlKeyword(string value)
    {
        return value.Equals("on", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("as", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("where", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("and", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("or", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("group", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("order", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("by", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("limit", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("offset", StringComparison.OrdinalIgnoreCase);
    }
}