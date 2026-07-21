-- Fix-WidgetLayout.sql
-- Writes a readable grid layout onto every system dashboard, keyed by each
-- dashboard's REAL widget ids, so widgets render at chart size and "Reset
-- layout" restores good sizes. Pure SQL - run directly:
--   psql "host=127.0.0.1 dbname=ppiq_presentation user=ppiq_dev password=ppiq_dev_local_only" -f Fix-WidgetLayout.sql
-- Backs up current layouts into ppiq_layout_backup first. Idempotent.

SET client_min_messages = warning;

CREATE TABLE IF NOT EXISTS public.ppiq_layout_backup (
    dashboard_id uuid, layout_json jsonb, saved_at timestamptz DEFAULT now(), tag text
);

-- backup current
INSERT INTO public.ppiq_layout_backup (dashboard_id, layout_json, tag)
SELECT id, layout_json, 'pre-sqlfix-' || to_char(now(),'YYYYMMDD_HH24MISS')
FROM public.dashboard_definitions WHERE is_deleted = false;

-- rebuild layout per dashboard from its real widgets
DO $$
DECLARE
    d RECORD;
    w RECORD;
    lg_items text;
    sm_items text;
    x int; y int; rowh int; ysm int;
    ww int; hh int; mw int; mh int;
    layout jsonb;
BEGIN
    FOR d IN SELECT id, dashboard_code FROM public.dashboard_definitions WHERE is_deleted = false LOOP
        lg_items := ''; sm_items := '';
        x := 0; y := 0; rowh := 0; ysm := 0;
        FOR w IN
            SELECT id, COALESCE(chart_type,'bar') AS ct, COALESCE(widget_type,'chart') AS wt
            FROM public.dashboard_widget_definitions
            WHERE dashboard_definition_id = d.id AND is_deleted = false
            ORDER BY sort_order NULLS LAST, created_at_utc
        LOOP
            IF lower(w.wt) = 'table' OR lower(w.ct) = 'table' THEN
                ww := 12; hh := 8; mw := 6; mh := 5;
            ELSIF lower(w.wt) = 'kpi' OR lower(w.ct) = 'kpi' THEN
                ww := 4; hh := 7; mw := 3; mh := 5;
            ELSE
                ww := 6; hh := 9; mw := 4; mh := 6;
            END IF;

            IF x + ww > 12 THEN x := 0; y := y + rowh; rowh := 0; END IF;

            lg_items := lg_items ||
                CASE WHEN lg_items = '' THEN '' ELSE ',' END ||
                jsonb_build_object('i', w.id::text, 'x', x, 'y', y, 'w', ww, 'h', hh, 'minW', mw, 'minH', mh)::text;

            sm_items := sm_items ||
                CASE WHEN sm_items = '' THEN '' ELSE ',' END ||
                jsonb_build_object('i', w.id::text, 'x', 0, 'y', ysm, 'w', 1, 'h', hh, 'minW', 1, 'minH', 5)::text;

            IF hh > rowh THEN rowh := hh; END IF;
            x := x + ww;
            ysm := ysm + hh;
        END LOOP;

        IF lg_items <> '' THEN
            layout := ('{"lg":[' || lg_items || '],"md":[' || lg_items || '],"sm":[' || sm_items ||
                       '],"xs":[' || sm_items || '],"xxs":[' || sm_items || ']}')::jsonb;
            UPDATE public.dashboard_definitions
            SET layout_json = layout, updated_at_utc = now()
            WHERE id = d.id;
            RAISE NOTICE 'sized %: % widgets', d.dashboard_code, (SELECT count(*) FROM public.dashboard_widget_definitions WHERE dashboard_definition_id = d.id AND is_deleted = false);
        END IF;
    END LOOP;
END $$;

-- verify
SELECT dashboard_code,
       jsonb_array_length(COALESCE(layout_json->'lg','[]'::jsonb)) AS lg_widgets
FROM public.dashboard_definitions
WHERE is_deleted = false
ORDER BY dashboard_code;
