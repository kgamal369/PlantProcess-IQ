const fs = require("node:fs");
const path = require("node:path");

const root = process.cwd();

const file = path.join(
  root,
  "Backend",
  "PlantProcess.Api",
  "Endpoints",
  "PageBuilder",
  "PageDefinitionEndpoints.cs"
);

let text = fs.readFileSync(file, "utf8");

const replacement = String.raw`    private static async Task EnsureSchemaAsync(PlantProcessDbContext db, CancellationToken cancellationToken)
    {
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        await EnsureOpenAsync(connection, cancellationToken);

        await using var command = new NpgsqlCommand(
            """
            CREATE EXTENSION IF NOT EXISTS pgcrypto;

            CREATE TABLE IF NOT EXISTS page_definitions (
                id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                tenant_id text NOT NULL DEFAULT 'demo',
                slug text NOT NULL,
                title text NOT NULL,
                owner_user_name text NOT NULL,
                visibility text NOT NULL DEFAULT 'Private',
                version integer NOT NULL DEFAULT 1,
                layout_json jsonb NOT NULL DEFAULT '{}'::jsonb,
                widget_bindings_json jsonb NOT NULL DEFAULT '{}'::jsonb,
                is_deleted boolean NOT NULL DEFAULT false,
                created_at_utc timestamptz NOT NULL DEFAULT now(),
                updated_at_utc timestamptz NOT NULL DEFAULT now(),
                CONSTRAINT ck_page_definitions_slug CHECK (slug ~ '^[a-z0-9]+(?:-[a-z0-9]+)*$'),
                CONSTRAINT ck_page_definitions_visibility CHECK (visibility IN ('Private', 'Shared', 'Public'))
            );

            CREATE UNIQUE INDEX IF NOT EXISTS ux_page_definitions_tenant_slug_active
            ON page_definitions (tenant_id, slug)
            WHERE is_deleted = false;

            CREATE INDEX IF NOT EXISTS ix_page_definitions_owner_visible
            ON page_definitions (tenant_id, owner_user_name, visibility)
            WHERE is_deleted = false;
            """,
            connection);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

`;

text = text.replace(
  /    private static async Task EnsureSchemaAsync\(PlantProcessDbContext db, CancellationToken cancellationToken\)\s*\{[\s\S]*?\n    \}\s*\n\s*private static async Task EnsureOpenAsync/,
  replacement + "    private static async Task EnsureOpenAsync"
);

if (!text.includes("await command.ExecuteNonQueryAsync(cancellationToken);")) {
  throw new Error("EnsureSchemaAsync was not replaced correctly.");
}

if (text.includes("await db.Database.ExecuteSqlRawAsync(")) {
  throw new Error("Old ExecuteSqlRawAsync call still exists.");
}

fs.writeFileSync(file, text.replace(/\r\n/g, "\n"), "utf8");

console.log("Fixed EnsureSchemaAsync to use raw NpgsqlCommand instead of ExecuteSqlRawAsync.");
