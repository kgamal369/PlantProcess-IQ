using System.Text.RegularExpressions;
using PlantProcess.Application.Integration.Contracts.Dtos;

namespace PlantProcess.Application.Integration.Security;

/// <summary>
/// Central read-only SQL safety validator.
/// PPIQ-T001/T004/T005/T006/T009:
/// - Uses token-boundary validation, so created_at / updated_at / created_by are valid.
/// - Rejects dangerous DDL/DML/admin/system SQL.
/// - Supports SELECT/WITH only.
/// - Supports dynamic registered table/view names through SqlAllowlistProvider.
/// </summary>
public static class SafeSqlValidator
{
    private static readonly string[] ForbiddenTokens =
    [
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
        "pg_read_file",
        "pg_read_binary_file",
        "pg_ls_dir",
        "pg_stat_file",
        "pg_read_server_files",
        "pg_logfile_rotate",
        "pg_reload_conf",
        "lo_import",
        "lo_export",
        "lo_create",
        "lo_unlink",
        "dblink",
        "dblink_exec",
        "dblink_send_query",
        "dblink_connect",
        "dblink_disconnect",
        "pg_sleep",
        "pg_sleep_for",
        "pg_sleep_until",
        "pg_catalog",
        "information_schema",
        "pg_proc",
        "pg_authid",
        "pg_shadow",
        "pg_roles",
        "pg_user",
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
    ];

    private static readonly HashSet<string> SystemSchemas = new(StringComparer.OrdinalIgnoreCase)
    {
        "pg_catalog",
        "information_schema",
        "sys",
        "master",
        "msdb",
        "tempdb"
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

    private static readonly Regex IdentifierPattern = new(
        @"""([^""]+)""|`([^`]+)`|([a-zA-Z_][a-zA-Z0-9_\.]*)",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static SqlSafetyValidationResultDto Validate(string? sqlText)
        => Validate(sqlText, null);

    public static SqlSafetyValidationResultDto Validate(string? sqlText, IEnumerable<string>? allowedTables)
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        var referencedTables = new List<string>();
        var allowlist = SqlAllowlistProvider.MergeWithDynamicNames(allowedTables);

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

            ValidateReferencedIdentifier(rawIdentifier, cteNames, allowlist, referencedTables, errors);
        }

        ValidateCommaJoinIdentifiers(sql, cteNames, allowlist, referencedTables, errors);

        if (Regex.IsMatch(lowered, @"\bwith\s+recursive\b", RegexOptions.IgnoreCase) &&
            !Regex.IsMatch(lowered, @"\blimit\s+\d+\b", RegexOptions.IgnoreCase))
        {
            errors.Add("WITH RECURSIVE queries must include an explicit LIMIT clause.");
        }

        if (referencedTables.Count == 0)
            warnings.Add("No FROM/JOIN table reference detected. This is allowed but may not be useful.");

        if (!Regex.IsMatch(lowered, @"\blimit\s+\d+\b", RegexOptions.IgnoreCase))
            warnings.Add("No LIMIT found. The preview endpoint should wrap the query and apply its own LIMIT.");

        var distinctTables = referencedTables
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new SqlSafetyValidationResultDto(
            errors.Count == 0,
            errors.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            distinctTables);
    }

    private static bool ContainsForbiddenToken(string loweredSql, string token)
    {
        var escaped = Regex.Escape(token).Replace(@"\ ", @"\s+");

        var pattern = token.Contains(' ', StringComparison.Ordinal)
            ? $@"(^|[^a-z0-9_]){escaped}([^a-z0-9_]|$)"
            : $@"(^|[^a-z0-9_]){escaped}([^a-z0-9_]|$|[ \t\r\n]*\()";

        return Regex.IsMatch(loweredSql, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static void ValidateReferencedIdentifier(
        string rawIdentifier,
        IReadOnlySet<string> cteNames,
        IReadOnlySet<string> allowlist,
        ICollection<string> referencedTables,
        ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(rawIdentifier))
            return;

        var cleaned = rawIdentifier.Trim().Trim('"').Trim('`');

        if (string.IsNullOrWhiteSpace(cleaned) || IsSqlKeyword(cleaned))
            return;

        var parts = cleaned
            .Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.Trim('"').Trim('`'))
            .ToArray();

        if (parts.Length == 0)
            return;

        if (parts.Length > 1 && SystemSchemas.Contains(parts[0]))
        {
            errors.Add($"System schema '{parts[0]}' is not allowed.");
            referencedTables.Add(cleaned);
            return;
        }

        var bare = SqlAllowlistProvider.NormalizeIdentifier(cleaned);

        if (string.IsNullOrWhiteSpace(bare) || cteNames.Contains(bare))
            return;

        referencedTables.Add(bare);

        if (!allowlist.Contains(bare))
            errors.Add($"Table or view '{cleaned}' is not in the configured SQL allowlist.");
    }

    private static void ValidateCommaJoinIdentifiers(
        string sql,
        IReadOnlySet<string> cteNames,
        IReadOnlySet<string> allowlist,
        ICollection<string> referencedTables,
        ICollection<string> errors)
    {
        foreach (Match match in Regex.Matches(
            sql,
            @"\bfrom\s+(.+?)(?:\bwhere\b|\bgroup\s+by\b|\border\s+by\b|\bhaving\b|\blimit\b|\boffset\b|$)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline))
        {
            var fromList = match.Groups[1].Value;

            if (!fromList.Contains(',', StringComparison.Ordinal))
                continue;

            foreach (var item in fromList.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var identifier = ExtractLeadingIdentifier(item);

                if (string.IsNullOrWhiteSpace(identifier))
                    continue;

                ValidateReferencedIdentifier(identifier, cteNames, allowlist, referencedTables, errors);
            }
        }
    }

    private static string? ExtractLeadingIdentifier(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
            return null;

        var trimmed = source.Trim();

        if (trimmed.StartsWith('('))
            return null;

        var match = Regex.Match(
            trimmed,
            @"^(?:""([^""]+)""|`([^`]+)`|([a-zA-Z_][a-zA-Z0-9_\.]*))",
            RegexOptions.CultureInvariant);

        if (!match.Success)
            return null;

        if (!string.IsNullOrWhiteSpace(match.Groups[1].Value))
            return match.Groups[1].Value;

        if (!string.IsNullOrWhiteSpace(match.Groups[2].Value))
            return match.Groups[2].Value;

        return match.Groups[3].Value;
    }

    private static IReadOnlySet<string> ExtractCteNames(string sql)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in Regex.Matches(
            sql,
            @"(?:\bwith\s+(?:recursive\s+)?|,)\s*(?:""([^""]+)""|`([^`]+)`|([a-zA-Z_][a-zA-Z0-9_]*))\s+as\s*\(",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            var value =
                !string.IsNullOrWhiteSpace(match.Groups[1].Value) ? match.Groups[1].Value :
                !string.IsNullOrWhiteSpace(match.Groups[2].Value) ? match.Groups[2].Value :
                match.Groups[3].Value;

            if (!string.IsNullOrWhiteSpace(value))
                names.Add(value);
        }

        return names;
    }

    private static string StripSqlComments(string sql)
    {
        return SafeSqlCommentStripper.Strip(sql);
    }

    private static string RemoveSingleTrailingSemicolon(string sql)
    {
        var trimmed = sql.TrimEnd();

        if (!trimmed.EndsWith(';'))
            return sql;

        var without = trimmed[..^1];

        return without.Contains(';') ? sql : without;
    }

    private static bool IsSqlKeyword(string value)
    {
        return value.Equals("select", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("from", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("join", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("where", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("group", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("by", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("order", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("having", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("limit", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("offset", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("as", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("on", StringComparison.OrdinalIgnoreCase);
    }
}

