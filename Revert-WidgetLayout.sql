-- Revert-WidgetLayout.sql - restore layouts saved by Fix-WidgetLayout.sql
SET client_min_messages = warning;
UPDATE public.dashboard_definitions d
SET layout_json = b.layout_json
FROM (
    SELECT DISTINCT ON (dashboard_id) dashboard_id, layout_json
    FROM public.ppiq_layout_backup
    ORDER BY dashboard_id, saved_at DESC
) b
WHERE d.id = b.dashboard_id;
SELECT 'reverted ' || count(*) FROM public.dashboard_definitions WHERE is_deleted=false;
