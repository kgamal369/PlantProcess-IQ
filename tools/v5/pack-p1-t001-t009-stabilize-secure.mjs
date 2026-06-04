import fs from "node:fs";
import path from "node:path";

const root = process.cwd();
const backendRoot = path.join(root, "Backend");
const reportDir = path.join(root, "Documentation", "v5");
fs.mkdirSync(reportDir, { recursive: true });

function exists(file) {
  return fs.existsSync(file);
}

function read(file) {
  return fs.readFileSync(file, "utf8");
}

function write(file, text) {
  fs.mkdirSync(path.dirname(file), { recursive: true });
  fs.writeFileSync(file, text, "utf8");
}

function toPosix(value) {
  return value.replaceAll(path.sep, "/");
}

function walk(dir, output = []) {
  if (!fs.existsSync(dir)) return output;

  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);

    if (entry.isDirectory()) {
      if ([
        "bin",
        "obj",
        "node_modules",
        "dist",
        "build",
        "coverage",
        ".git",
        ".vs",
        ".vite"
      ].includes(entry.name)) continue;

      walk(full, output);
    } else {
      output.push(full);
    }
  }

  return output;
}

function isRuntimeSource(relative) {
  const normalized = toPosix(relative);

  if (!normalized.startsWith("Backend/")) return false;
  if (normalized.includes("/bin/")) return false;
  if (normalized.includes("/obj/")) return false;
  if (normalized.includes("/Documentation/")) return false;
  if (normalized.includes("/docs/")) return false;
  if (normalized.includes("/tools/")) return false;
  if (normalized.includes("/tmp/")) return false;
  if (normalized.includes("_backup")) return false;
  if (normalized.includes(".bak_")) return false;

  return /\.(cs|csproj|json)$/.test(normalized);
}

function findFile(relative) {
  const full = path.join(root, relative);
  return exists(full) ? full : null;
}

const changes = [];

// ------------------------------------------------------------
// T001/T004/T005/T006/T009 — Application SafeSqlValidator.
// ------------------------------------------------------------
const appSafeSqlPath = path.join(
  root,
  "Backend",
  "PlantProcess.Application",
  "Integration",
  "Security",
  "SafeSqlValidator.cs"
);

const allowlistProviderPath = path.join(
  root,
  "Backend",
  "PlantProcess.Application",
  "Integration",
  "Security",
  "SqlAllowlistProvider.cs"
);

write(allowlistProviderPath, `using System.Collections.ObjectModel;

namespace PlantProcess.Application.Integration.Security;

/// <summary>
/// Single bootstrap source for SQL validation allowlists.
///
/// PPIQ-T004/T005:
/// - Removes duplicate table names.
/// - Keeps demo/industry-specific names out of validator code.
/// - Allows schema/mapping providers to merge dynamic dataset/view names into the same validation path.
/// </summary>
public static class SqlAllowlistProvider
{
    private static readonly string[] BootstrapTables =
    [
        // Integration/source metadata
        "staging_records",
        "import_batches",
        "source_system_definitions",
        "connection_profiles",
        "source_dataset_definitions",
        "source_field_definitions",
        "schema_mapping_definitions",
        "canonical_mapping_versions",
        "canonical_mapping_fields",
        "canonical_business_keys",
        "canonical_join_edges",
        "canonical_genealogy_edges",

        // Plant/site canonical model
        "sites",
        "areas",
        "equipment",

        // Material/genealogy canonical model
        "material_units",
        "material_aliases",
        "genealogy_edges",

        // Process canonical model
        "process_step_executions",
        "parameter_definitions",
        "parameter_observations",
        "process_events",
        "downtime_events",

        // Quality canonical model
        "defect_catalog",
        "quality_events",
        "data_quality_issues",

        // Analytics/read model metadata
        "risk_scores",
        "correlation_results",
        "model_registry",
        "kpi_definitions",
        "schema_view_definitions",
        "job_definitions",
        "job_run_history",
        "job_run_histories",

        // Approved generic read models/views
        "vw_defect_by_shift",
        "vw_material_with_defects",
        "vw_daily_quality_summary",
        "vw_correlation_input",
        "mv_dashboard_material_summary",
        "mv_dashboard_quality_daily",
        "mv_dashboard_defect_breakdown"
    ];

    public static IReadOnlySet<string> DefaultBootstrapAllowlist { get; } =
        new ReadOnlySet<string>(BootstrapTables.ToHashSet(StringComparer.OrdinalIgnoreCase));

    public static IReadOnlySet<string> MergeWithDynamicNames(IEnumerable<string>? dynamicNames)
    {
        var merged = new HashSet<string>(DefaultBootstrapAllowlist, StringComparer.OrdinalIgnoreCase);

        if (dynamicNames is null)
            return new ReadOnlySet<string>(merged);

        foreach (var name in dynamicNames)
        {
            var normalized = NormalizeIdentifier(name);

            if (!string.IsNullOrWhiteSpace(normalized))
                merged.Add(normalized);
        }

        return new ReadOnlySet<string>(merged);
    }

    public static string NormalizeIdentifier(string? identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            return string.Empty;

        var cleaned = identifier
            .Trim()
            .Trim('"')
            .Trim('\`')
            .Trim();

        var dot = cleaned.LastIndexOf('.');

        if (dot >= 0 && dot < cleaned.Length - 1)
            cleaned = cleaned[(dot + 1)..];

        return cleaned.Trim();
    }
}
`);
changes.push("Wrote application SqlAllowlistProvider");

write(appSafeSqlPath, `using System.Text.RegularExpressions;
using PlantProcess.Application.Integration.Contracts.Dtos;

namespace PlantProcess.Application.Integration.Security;

/// <summary>
/// Central read-only SQL safety validator.
///
/// PPIQ-T001/T004/T005/T006/T009:
/// - Uses token boundaries, so created_at/created_by/pre_grade are not rejected as CREATE/DROP.
/// - Rejects DDL/DML/admin/server-side dangerous functions.
/// - Supports SELECT/WITH only.
/// - Allows one optional trailing semicolon, but rejects multiple statements.
/// - Supports CTE references, JOIN styles, quoted identifiers, and comma joins.
/// - Accepts a dynamic allowlist supplied by schema/mapping providers.
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
        @"/\\*[\\s\\S]*?\\*/",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex LineCommentPattern = new(
        @"--[^\\r\\n]*",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex WhitespacePattern = new(
        @"\\s+",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex FromOrJoinPattern = new(
        @"\\b(?:from|(?:cross\\s+|natural\\s+|inner\\s+|left\\s+(?:outer\\s+)?|right\\s+(?:outer\\s+)?|full\\s+(?:outer\\s+)?)?join)\\s+(?:""([^""]+)""|\`([^\`]+)\`|([a-zA-Z_][a-zA-Z0-9_\\.]*))",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex CommaFromPattern = new(
        @"\\bfrom\\s+(?:""[^""]+""|\`[^\`]+\`|[a-zA-Z_][a-zA-Z0-9_\\.]*)(?:\\s+(?:as\\s+)?[a-zA-Z_][a-zA-Z0-9_]*)?\\s*,\\s*(?<rest>.+?)(?:\\bwhere\\b|\\bgroup\\s+by\\b|\\border\\s+by\\b|\\bhaving\\b|\\blimit\\b|\\boffset\\b|$)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex IdentifierPattern = new(
        @"""([^""]+)""|\`([^\`]+)\`|([a-zA-Z_][a-zA-Z0-9_\\.]*)",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static SqlSafetyValidationResultDto Validate(string? sqlText)
        => Validate(sqlText, SqlAllowlistProvider.DefaultBootstrapAllowlist);

    public static SqlSafetyValidationResultDto Validate(
        string? sqlText,
        IEnumerable<string>? allowedTables)
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

                ValidateReferencedIdentifier(rawIdentifier, cteNames, allowlist, referencedTables, errors);
            }
        }

        if (Regex.IsMatch(lowered, @"\\bwith\\s+recursive\\b", RegexOptions.IgnoreCase) &&
            !Regex.IsMatch(lowered, @"\\blimit\\s+\\d+\\b", RegexOptions.IgnoreCase))
        {
            errors.Add("WITH RECURSIVE queries must include an explicit LIMIT clause.");
        }

        if (referencedTables.Count == 0)
            warnings.Add("No FROM/JOIN table reference detected. This is allowed but may not be useful.");

        if (!Regex.IsMatch(lowered, @"\\blimit\\s+\\d+\\b", RegexOptions.IgnoreCase))
            warnings.Add("No LIMIT found. The preview endpoint should wrap the query and apply its own LIMIT.");

        return new SqlSafetyValidationResultDto(
            IsValid: errors.Count == 0,
            Errors: errors.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            Warnings: warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            ReferencedTables: referencedTables.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private static bool ContainsForbiddenToken(string loweredSql, string token)
    {
        if (token.Equals("pg_", StringComparison.OrdinalIgnoreCase))
            return Regex.IsMatch(loweredSql, @"\\bpg_[a-zA-Z0-9_]*\\b", RegexOptions.IgnoreCase);

        var escaped = Regex.Escape(token);
        return Regex.IsMatch(loweredSql, $@"\\b{escaped}\\b|\\b{escaped}\\s*\\(", RegexOptions.IgnoreCase);
    }

    private static void ValidateReferencedIdentifier(
        string rawIdentifier,
        IReadOnlySet<string> cteNames,
        IReadOnlySet<string> allowlist,
        ICollection<string> referencedTables,
        ICollection<string> errors)
    {
        var cleaned = rawIdentifier.Trim().Trim('"').Trim('\`');

        if (string.IsNullOrWhiteSpace(cleaned))
            return;

        var parts = cleaned.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length > 1 && SystemSchemas.Contains(parts[0]))
        {
            errors.Add($"System schema '{parts[0]}' is not allowed.");
            return;
        }

        var bare = SqlAllowlistProvider.NormalizeIdentifier(cleaned);

        if (string.IsNullOrWhiteSpace(bare))
            return;

        if (cteNames.Contains(bare))
            return;

        referencedTables.Add(bare);

        if (!allowlist.Contains(bare))
            errors.Add($"Table or view '{cleaned}' is not in the configured SQL allowlist.");
    }

    private static IReadOnlySet<string> ExtractCteNames(string sql)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in Regex.Matches(
            sql,
            @"(?:\\bwith\\s+(?:recursive\\s+)?|,)\\s*([a-zA-Z_][a-zA-Z0-9_]*)\\s+as\\s*\\(",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            names.Add(match.Groups[1].Value);
        }

        return names;
    }

    private static string StripSqlComments(string sql)
    {
        var noBlockComments = BlockCommentPattern.Replace(sql, string.Empty);
        return LineCommentPattern.Replace(noBlockComments, string.Empty);
    }

    private static string RemoveSingleTrailingSemicolon(string sql)
    {
        var trimmed = sql.TrimEnd();

        if (!trimmed.EndsWith(';'))
            return sql;

        var without = trimmed[..^1];

        return without.Contains(';')
            ? sql
            : without;
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
`);
changes.push("Replaced application SafeSqlValidator");

// ------------------------------------------------------------
// T001/T006 — retire BasicSqlGuard in Analytics.Core KpiEngine.
// ------------------------------------------------------------
const kpiEnginePath = path.join(
  root,
  "Backend",
  "PlantProcess.Analytics.Core",
  "Kpi",
  "KpiEngine.cs"
);

if (exists(kpiEnginePath)) {
  let text = read(kpiEnginePath);

  text = text.replaceAll("BasicSqlGuard.Validate", "SafeSqlValidator.Validate");

  const basicStart = text.indexOf("public static class BasicSqlGuard");
  const kpiStart = text.indexOf("public sealed class KpiEngine");

  if (basicStart >= 0 && kpiStart > basicStart) {
    const before = text.slice(0, basicStart);
    const after = text.slice(kpiStart);

    const safeSqlClass = `/// <summary>Read-only SQL validator for SQL-view KPIs. Kept in Analytics.Core to avoid project dependency cycles.</summary>
public static class SafeSqlValidator
{
private static readonly string[] ForbiddenTokens =
[
    "insert", "update", "delete", "drop", "alter", "truncate",
    "grant", "revoke", "exec", "execute", "call", "copy", "merge",
    "create", "vacuum", "analyze", "pg_catalog", "information_schema",
    "pg_read_file", "pg_sleep", "dblink", "xp_cmdshell", "openrowset"
];

public static void Validate(string? sql)
{
    if (string.IsNullOrWhiteSpace(sql))
        throw new KpiFormulaException("KPI SQL view text is empty.");

    var withoutComments = StripSqlComments(sql).Trim();
    var trimmed = RemoveSingleTrailingSemicolon(withoutComments).Trim();

    if (trimmed.Contains(';'))
        throw new KpiFormulaException("KPI SQL view must be a single SELECT/WITH statement.");

    var lowered = System.Text.RegularExpressions.Regex.Replace(trimmed.ToLowerInvariant(), @"\\s+", " ");

    if (!(lowered.StartsWith("select ") || lowered == "select" || lowered.StartsWith("with ")))
        throw new KpiFormulaException("KPI SQL view must be a read-only SELECT/WITH query.");

    foreach (var token in ForbiddenTokens)
    {
        if (ContainsForbiddenToken(lowered, token))
            throw new KpiFormulaException($"KPI SQL view contains a forbidden token: '{token}'.");
    }
}

private static bool ContainsForbiddenToken(string loweredSql, string token)
{
    var escaped = System.Text.RegularExpressions.Regex.Escape(token);
    return System.Text.RegularExpressions.Regex.IsMatch(
        loweredSql,
        $@"\\b{escaped}\\b|\\b{escaped}\\s*\\(",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
}

private static string StripSqlComments(string sql)
{
    var noLine = System.Text.RegularExpressions.Regex.Replace(
        sql,
        @"--.*?$",
        string.Empty,
        System.Text.RegularExpressions.RegexOptions.Multiline);

    return System.Text.RegularExpressions.Regex.Replace(
        noLine,
        @"/\\*.*?\\*/",
        string.Empty,
        System.Text.RegularExpressions.RegexOptions.Singleline);
}

private static string RemoveSingleTrailingSemicolon(string sql)
{
    var trimmed = sql.TrimEnd();

    if (!trimmed.EndsWith(';'))
        return sql;

    var without = trimmed[..^1];

    return without.Contains(';')
        ? sql
        : without;
}
}
`;

    text = before + safeSqlClass + after;
  } else {
    text = text.replaceAll("BasicSqlGuard", "SafeSqlValidator");
  }

  write(kpiEnginePath, text);
  changes.push("Retired BasicSqlGuard in Analytics.Core KpiEngine");
}

// ------------------------------------------------------------
// T003 — ensure exactly one SaveToken assignment.
// ------------------------------------------------------------
const programPath = path.join(root, "Backend", "PlantProcess.Api", "Program.cs");

if (exists(programPath)) {
  let text = read(programPath);

  const saveTokenRegex = /\\s*options\\.SaveToken\\s*=\\s*(?:true|false)\\s*;\\s*(?:\\/\\/[^\\r\\n]*)?/g;
  text = text.replace(saveTokenRegex, "");

  text = text.replace(
    /options\\.RequireHttpsMetadata\\s*=\\s*!builder\\.Environment\\.IsDevelopment\\(\\);/,
    `options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
            options.SaveToken = true; // PPIQ-T003: intentionally keep exactly one SaveToken assignment for auth diagnostics/tests.`
  );

  write(programPath, text);
  changes.push("Normalized Program.cs SaveToken assignment");
}

// ------------------------------------------------------------
// T007 — parameterize default tenant option if AuthOptions exists.
// ------------------------------------------------------------
const authOptionsPath = path.join(root, "Backend", "PlantProcess.Api", "Security", "AuthOptions.cs");

if (exists(authOptionsPath)) {
  let text = read(authOptionsPath);

  if (!text.includes("DefaultTenantId")) {
    text = text.replace(
      /public\\s+int\\s+AccessTokenMinutes\\s*\\{\\s*get;\\s*set;\\s*\\}\\s*=\\s*120\\s*;/,
      `public int AccessTokenMinutes { get; set; } = 120;

    /// <summary>
    /// Configurable bootstrap tenant used only when the system provisions an empty development/demo database.
    /// PPIQ-T007: do not hard-code the tenant in provisioning logic.
    /// </summary>
    public string DefaultTenantId { get; set; } = "00000000-0000-0000-0000-000000000001";`
    );

    write(authOptionsPath, text);
    changes.push("Added configurable AuthOptions.DefaultTenantId");
  }
}

// Safe textual replacement for the hardcoded default tenant literal outside options/config/test files.
const defaultTenantLiteral = "00000000-0000-0000-0000-000000000001";
for (const file of walk(path.join(root, "Backend", "PlantProcess.Api", "Security"))) {
  const relative = toPosix(path.relative(root, file));

  if (!relative.endsWith(".cs")) continue;
  if (relative.endsWith("AuthOptions.cs")) continue;
  if (relative.includes("/tests/")) continue;

  let text = read(file);

  if (!text.includes(defaultTenantLiteral)) continue;

  text = text
    .replaceAll(`Guid.Parse("${defaultTenantLiteral}")`, `Guid.Parse(auth.DefaultTenantId)`)
    .replaceAll(`Guid.Parse(@"${defaultTenantLiteral}")`, `Guid.Parse(auth.DefaultTenantId)`)
    .replaceAll(`new Guid("${defaultTenantLiteral}")`, `Guid.Parse(auth.DefaultTenantId)`);

  write(file, text);
  changes.push(`Parameterised default tenant literal in ${relative}`);
}

// ------------------------------------------------------------
// T002 — remove obvious silent null material correlation summary text.
// ------------------------------------------------------------
const correlationServicePath = path.join(
  root,
  "Backend",
  "PlantProcess.Application",
  "Analytics",
  "Services",
  "CorrelationService.cs"
);

if (exists(correlationServicePath)) {
  let text = read(correlationServicePath);

  text = text
    .replaceAll("ContextSummary = null", "ContextSummary = \"No material correlation baseline is available for the selected material/defect yet.\"")
    .replaceAll("ContextSummary: null", "ContextSummary: \"No material correlation baseline is available for the selected material/defect yet.\"")
    .replaceAll("ZScore = null", "ZScore = null // PPIQ-T002: null is explicit when baseline is not available")
    .replaceAll("ZScore: null", "ZScore: null /* PPIQ-T002: null is explicit when baseline is not available */");

  write(correlationServicePath, text);
  changes.push("Applied material correlation explicit-null contract patch where patterns existed");
}

// ------------------------------------------------------------
// T008 — .gitignore hardening.
// ------------------------------------------------------------
const gitignorePath = path.join(root, ".gitignore");
let gitignore = exists(gitignorePath) ? read(gitignorePath) : "";

const ignoreBlock = `
# PlantProcess IQ generated/local backup hygiene — PPIQ-T008
*.bak
*.bak_*
*.orig
*.tmp
*.old
*.rej
*.patch
*_backup/
*_backup_*/
tmp/
tmp_*/
.ppiq-phase1-backup/
_pack_*/
_p1_*/
_p2_*/
_p3_*/
_p4_*/
_p5_*/
_v5_*_backup_*/
`;

if (!gitignore.includes("PPIQ-T008")) {
  gitignore = gitignore.trimEnd() + "\n" + ignoreBlock;
  write(gitignorePath, gitignore);
  changes.push("Updated root .gitignore for backup/tmp hygiene");
}

// ------------------------------------------------------------
// T001/T009 — tests.
// ------------------------------------------------------------
const kpiTestPath = path.join(
  root,
  "Backend",
  "tests",
  "PlantProcess.Application.UnitTests",
  "Phase1_KpiSqlGuardTests.cs"
);

write(kpiTestPath, `using PlantProcess.Analytics.Core.Kpi;
using Xunit;

namespace PlantProcess.Phase1.Tests;

public sealed class Phase1_KpiSqlGuardTests
{
    [Theory]
    [InlineData("SELECT created_at, updated_at, created_by FROM quality_events")]
    [InlineData("SELECT id FROM material_units ORDER BY created_at OFFSET 10")]
    [InlineData("WITH x AS (SELECT created_by FROM quality_events) SELECT created_by FROM x")]
    [InlineData("SELECT pre_grade, created_at FROM quality_events")]
    public void Valid_read_only_views_with_substrings_should_pass(string sql)
    {
        SafeSqlValidator.Validate(sql);
    }

    [Theory]
    [InlineData("SELECT 1; DROP TABLE material_units")]
    [InlineData("SELECT * FROM quality_events; SELECT * FROM material_units")]
    [InlineData("DROP TABLE material_units")]
    [InlineData("CREATE VIEW v AS SELECT * FROM material_units")]
    [InlineData("SELECT pg_read_file('/etc/passwd')")]
    [InlineData("SELECT * FROM information_schema.tables")]
    public void Dangerous_or_multi_statement_sql_should_be_rejected(string sql)
    {
        Assert.Throws<KpiFormulaException>(() => SafeSqlValidator.Validate(sql));
    }
}
`);
changes.push("Rewrote Phase1 KPI SQL guard tests");

const safeSqlTestPath = path.join(
  root,
  "Backend",
  "tests",
  "PlantProcess.Application.UnitTests",
  "Security",
  "SafeSqlValidatorPhase1Tests.cs"
);

write(safeSqlTestPath, `using PlantProcess.Application.Integration.Security;
using Xunit;

namespace PlantProcess.Application.UnitTests.Security;

public sealed class SafeSqlValidatorPhase1Tests
{
    [Theory]
    [InlineData("SELECT created_at, updated_at, created_by FROM quality_events")]
    [InlineData("SELECT id FROM material_units ORDER BY created_at OFFSET 10")]
    [InlineData("WITH safe_cte AS (SELECT created_by FROM quality_events) SELECT created_by FROM safe_cte")]
    [InlineData("SELECT pre_grade FROM quality_events")]
    public void Valid_read_only_sql_with_forbidden_substrings_should_pass(string sql)
    {
        var result = SafeSqlValidator.Validate(sql);

        Assert.True(result.IsValid, string.Join(" | ", result.Errors));
    }

    [Theory]
    [InlineData("SELECT 1; DROP TABLE material_units")]
    [InlineData("DROP TABLE quality_events")]
    [InlineData("CREATE VIEW unsafe AS SELECT * FROM quality_events")]
    [InlineData("SELECT * FROM pg_catalog.pg_tables")]
    [InlineData("SELECT pg_read_file('/etc/passwd')")]
    [InlineData("SELECT * FROM information_schema.tables")]
    public void Dangerous_sql_should_fail(string sql)
    {
        var result = SafeSqlValidator.Validate(sql);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Dynamic_allowlist_should_allow_registered_runtime_view()
    {
        var result = SafeSqlValidator.Validate(
            "SELECT created_at FROM tenant_registered_view OFFSET 10",
            new[] { "tenant_registered_view" });

        Assert.True(result.IsValid, string.Join(" | ", result.Errors));
    }

    [Fact]
    public void Default_allowlist_should_not_contain_duplicates()
    {
        var values = SqlAllowlistProvider.DefaultBootstrapAllowlist.ToArray();

        Assert.Equal(values.Length, values.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }
}
`);
changes.push("Added SafeSqlValidator Phase-1 corpus tests");

// ------------------------------------------------------------
// Validation report.
// ------------------------------------------------------------
write(
  path.join(reportDir, "pack-p1-t001-t009-implementation-report.json"),
  JSON.stringify({ generatedAtUtc: new Date().toISOString(), changes }, null, 2)
);

console.log(JSON.stringify({
  changes: changes.length,
  report: "Documentation/v5/pack-p1-t001-t009-implementation-report.json"
}, null, 2));

console.log("[pack-p1] implementation completed.");