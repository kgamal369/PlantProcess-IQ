const fs = require("node:fs");
const path = require("node:path");

const file = path.join(
  process.cwd(),
  "Backend",
  "database",
  "scripts",
  "207_fix_dashboard_widget_expression_smallint_schema_drift.sql"
);

const sql = String.raw`\set ON_ERROR_STOP on

BEGIN;

-- ============================================================================
-- PlantProcess IQ
-- 207_fix_dashboard_widget_expression_smallint_schema_drift.sql
--
-- Purpose:
--   Repair dashboard_widget_definitions local schema drift where EF expects
--   smallint/boolean/jsonb/integer but old local DB columns may still be text.
-- ============================================================================

DO $$
DECLARE
    col_type text;
BEGIN
    -- expression_version: EF expects smallint
    SELECT data_type
    INTO col_type
    FROM information_schema.columns
    WHERE table_schema = 'public'
      AND table_name = 'dashboard_widget_definitions'
      AND column_name = 'expression_version';

    IF col_type IN ('text', 'character varying', 'character') THEN
        ALTER TABLE public.dashboard_widget_definitions
            ALTER COLUMN expression_version DROP DEFAULT;

        ALTER TABLE public.dashboard_widget_definitions
            ALTER COLUMN expression_version TYPE smallint
            USING COALESCE(NULLIF(regexp_replace(expression_version::text, '[^0-9-]', '', 'g'), '')::smallint, 1);

        ALTER TABLE public.dashboard_widget_definitions
            ALTER COLUMN expression_version SET DEFAULT 1;

        UPDATE public.dashboard_widget_definitions
        SET expression_version = 1
        WHERE expression_version IS NULL;

        ALTER TABLE public.dashboard_widget_definitions
            ALTER COLUMN expression_version SET NOT NULL;

        RAISE NOTICE 'Converted dashboard_widget_definitions.expression_version from % to smallint', col_type;
    END IF;

    -- expression_last_validation_status: EF enum conversion expects smallint
    SELECT data_type
    INTO col_type
    FROM information_schema.columns
    WHERE table_schema = 'public'
      AND table_name = 'dashboard_widget_definitions'
      AND column_name = 'expression_last_validation_status';

    IF col_type IN ('text', 'character varying', 'character') THEN
        ALTER TABLE public.dashboard_widget_definitions
            ALTER COLUMN expression_last_validation_status DROP DEFAULT;

        ALTER TABLE public.dashboard_widget_definitions
            ALTER COLUMN expression_last_validation_status TYPE smallint
            USING CASE
                WHEN NULLIF(TRIM(expression_last_validation_status::text), '') IS NULL THEN 0::smallint
                WHEN LOWER(TRIM(expression_last_validation_status::text)) IN ('pending', '0') THEN 0::smallint
                WHEN LOWER(TRIM(expression_last_validation_status::text)) IN ('valid', '1') THEN 1::smallint
                WHEN LOWER(TRIM(expression_last_validation_status::text)) IN ('invalid', '2') THEN 2::smallint
                WHEN LOWER(TRIM(expression_last_validation_status::text)) IN ('warning', '3') THEN 3::smallint
                ELSE COALESCE(NULLIF(regexp_replace(expression_last_validation_status::text, '[^0-9-]', '', 'g'), '')::smallint, 0::smallint)
            END;

        ALTER TABLE public.dashboard_widget_definitions
            ALTER COLUMN expression_last_validation_status SET DEFAULT 0;

        UPDATE public.dashboard_widget_definitions
        SET expression_last_validation_status = 0
        WHERE expression_last_validation_status IS NULL;

        ALTER TABLE public.dashboard_widget_definitions
            ALTER COLUMN expression_last_validation_status SET NOT NULL;

        RAISE NOTICE 'Converted dashboard_widget_definitions.expression_last_validation_status from % to smallint', col_type;
    END IF;

    -- expression_enabled: EF expects boolean
    SELECT data_type
    INTO col_type
    FROM information_schema.columns
    WHERE table_schema = 'public'
      AND table_name = 'dashboard_widget_definitions'
      AND column_name = 'expression_enabled';

    IF col_type IN ('text', 'character varying', 'character') THEN
        ALTER TABLE public.dashboard_widget_definitions
            ALTER COLUMN expression_enabled DROP DEFAULT;

        ALTER TABLE public.dashboard_widget_definitions
            ALTER COLUMN expression_enabled TYPE boolean
            USING CASE LOWER(TRIM(expression_enabled::text))
                WHEN 'true' THEN true
                WHEN 't' THEN true
                WHEN '1' THEN true
                WHEN 'yes' THEN true
                WHEN 'y' THEN true
                ELSE false
            END;

        ALTER TABLE public.dashboard_widget_definitions
            ALTER COLUMN expression_enabled SET DEFAULT false;

        UPDATE public.dashboard_widget_definitions
        SET expression_enabled = false
        WHERE expression_enabled IS NULL;

        ALTER TABLE public.dashboard_widget_definitions
            ALTER COLUMN expression_enabled SET NOT NULL;

        RAISE NOTICE 'Converted dashboard_widget_definitions.expression_enabled from % to boolean', col_type;
    END IF;

    -- advanced_expression_json: EF expects jsonb
    SELECT data_type
    INTO col_type
    FROM information_schema.columns
    WHERE table_schema = 'public'
      AND table_name = 'dashboard_widget_definitions'
      AND column_name = 'advanced_expression_json';

    IF col_type IN ('text', 'character varying', 'character') THEN
        ALTER TABLE public.dashboard_widget_definitions
            ALTER COLUMN advanced_expression_json DROP DEFAULT;

        ALTER TABLE public.dashboard_widget_definitions
            ALTER COLUMN advanced_expression_json TYPE jsonb
            USING CASE
                WHEN NULLIF(TRIM(advanced_expression_json::text), '') IS NULL THEN '{}'::jsonb
                ELSE advanced_expression_json::jsonb
            END;

        ALTER TABLE public.dashboard_widget_definitions
            ALTER COLUMN advanced_expression_json SET DEFAULT '{}'::jsonb;

        UPDATE public.dashboard_widget_definitions
        SET advanced_expression_json = '{}'::jsonb
        WHERE advanced_expression_json IS NULL;

        ALTER TABLE public.dashboard_widget_definitions
            ALTER COLUMN advanced_expression_json SET NOT NULL;

        RAISE NOTICE 'Converted dashboard_widget_definitions.advanced_expression_json from % to jsonb', col_type;
    END IF;
END $$;

COMMIT;

SELECT
    column_name,
    data_type,
    udt_name,
    is_nullable,
    column_default
FROM information_schema.columns
WHERE table_schema = 'public'
  AND table_name = 'dashboard_widget_definitions'
  AND column_name IN (
      'sort_order',
      'expression_version',
      'expression_last_validation_status',
      'expression_enabled',
      'advanced_expression_json',
      'filter_json',
      'layout_json',
      'display_options_json'
  )
ORDER BY column_name;
`;

fs.writeFileSync(file, sql.replace(/\r\n/g, "\n"), "utf8");

console.log("Wrote BOM-free SQL drift repair:");
console.log(file);
