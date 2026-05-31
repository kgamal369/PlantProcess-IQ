\set ON_ERROR_STOP on

BEGIN;

-- ============================================================================
-- PlantProcess IQ
-- 206_fix_dashboard_widget_definition_schema_drift.sql
--
-- Purpose:
--   Repair local/demo DB schema drift where dashboard widget numeric/json/boolean
--   columns were created or left as text by earlier manual scripts.
--
-- Why:
--   EF Core materializes DashboardWidgetDefinition using typed .NET properties.
--   If a typed property column is text, Npgsql throws:
--   "Reading as System.Int16 is not supported for fields having DataTypeName 'text'"
-- ============================================================================

DO $$
DECLARE
    col_name text;
    col_type text;
BEGIN
    -- ------------------------------------------------------------------------
    -- Integer-like dashboard widget columns.
    -- sort_order is confirmed by the domain model/configuration.
    -- The other names are guarded by EXISTS checks for future/layout variants.
    -- ------------------------------------------------------------------------
    FOREACH col_name IN ARRAY ARRAY[
        'sort_order',
        'display_order',
        'grid_x',
        'grid_y',
        'grid_w',
        'grid_h',
        'x',
        'y',
        'w',
        'h',
        'row',
        'column',
        'col',
        'row_span',
        'column_span',
        'col_span',
        'width_units',
        'height_units'
    ]
    LOOP
        SELECT data_type
        INTO col_type
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'dashboard_widget_definitions'
          AND column_name = col_name;

        IF col_type IN ('text', 'character varying', 'character') THEN
            EXECUTE format(
                'ALTER TABLE public.dashboard_widget_definitions ALTER COLUMN %I DROP DEFAULT',
                col_name
            );

            EXECUTE format(
                'ALTER TABLE public.dashboard_widget_definitions ALTER COLUMN %I TYPE integer USING COALESCE(NULLIF(regexp_replace(%I::text, ''[^0-9-]'', '''', ''g''), '''')::integer, 0)',
                col_name,
                col_name
            );

            RAISE NOTICE 'Converted %.% from % to integer',
                'dashboard_widget_definitions',
                col_name,
                col_type;
        END IF;
    END LOOP;

    -- ------------------------------------------------------------------------
    -- JSONB columns.
    -- ------------------------------------------------------------------------
    FOREACH col_name IN ARRAY ARRAY[
        'filter_json',
        'layout_json',
        'display_options_json'
    ]
    LOOP
        SELECT data_type
        INTO col_type
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'dashboard_widget_definitions'
          AND column_name = col_name;

        IF col_type IN ('text', 'character varying', 'character') THEN
            EXECUTE format(
                'ALTER TABLE public.dashboard_widget_definitions ALTER COLUMN %I DROP DEFAULT',
                col_name
            );

            EXECUTE format(
                'ALTER TABLE public.dashboard_widget_definitions ALTER COLUMN %I TYPE jsonb USING CASE WHEN NULLIF(TRIM(%I::text), '''') IS NULL THEN ''{}''::jsonb ELSE %I::jsonb END',
                col_name,
                col_name,
                col_name
            );

            EXECUTE format(
                'ALTER TABLE public.dashboard_widget_definitions ALTER COLUMN %I SET DEFAULT ''{}''::jsonb',
                col_name
            );

            RAISE NOTICE 'Converted %.% from % to jsonb',
                'dashboard_widget_definitions',
                col_name,
                col_type;
        END IF;
    END LOOP;

    -- ------------------------------------------------------------------------
    -- Boolean columns.
    -- ------------------------------------------------------------------------
    FOREACH col_name IN ARRAY ARRAY[
        'is_active',
        'is_deleted',
        'is_synthetic'
    ]
    LOOP
        SELECT data_type
        INTO col_type
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'dashboard_widget_definitions'
          AND column_name = col_name;

        IF col_type IN ('text', 'character varying', 'character') THEN
            EXECUTE format(
                'ALTER TABLE public.dashboard_widget_definitions ALTER COLUMN %I DROP DEFAULT',
                col_name
            );

            EXECUTE format(
                'ALTER TABLE public.dashboard_widget_definitions ALTER COLUMN %I TYPE boolean USING CASE LOWER(TRIM(%I::text))
                    WHEN ''true'' THEN true
                    WHEN ''t'' THEN true
                    WHEN ''1'' THEN true
                    WHEN ''yes'' THEN true
                    WHEN ''y'' THEN true
                    WHEN ''false'' THEN false
                    WHEN ''f'' THEN false
                    WHEN ''0'' THEN false
                    WHEN ''no'' THEN false
                    WHEN ''n'' THEN false
                    ELSE false
                END',
                col_name,
                col_name
            );

            EXECUTE format(
                'ALTER TABLE public.dashboard_widget_definitions ALTER COLUMN %I SET DEFAULT false',
                col_name
            );

            RAISE NOTICE 'Converted %.% from % to boolean',
                'dashboard_widget_definitions',
                col_name,
                col_type;
        END IF;
    END LOOP;
END $$;

-- Keep canonical dashboard widget defaults safe.
ALTER TABLE public.dashboard_widget_definitions
    ALTER COLUMN sort_order SET DEFAULT 0;

UPDATE public.dashboard_widget_definitions
SET sort_order = 0
WHERE sort_order IS NULL;

ALTER TABLE public.dashboard_widget_definitions
    ALTER COLUMN sort_order SET NOT NULL;

COMMIT;

SELECT
    column_name,
    data_type,
    udt_name
FROM information_schema.columns
WHERE table_schema = 'public'
  AND table_name = 'dashboard_widget_definitions'
  AND column_name IN (
      'sort_order',
      'filter_json',
      'layout_json',
      'display_options_json',
      'is_active',
      'is_deleted',
      'is_synthetic'
  )
ORDER BY column_name;
