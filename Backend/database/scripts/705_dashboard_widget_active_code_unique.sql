-- The system dashboard template seed upserts widgets with
-- ON CONFLICT (widget_code) WHERE is_deleted = FALSE, which requires a matching
-- partial unique index. Active widget codes are unique by design. Guarded and
-- idempotent; a defensive soft-dedup keeps the lowest id active so the index
-- can always be created.
DO $widx$
BEGIN
    IF to_regclass('public.dashboard_widget_definitions') IS NULL THEN
        RETURN;
    END IF;

    UPDATE public.dashboard_widget_definitions d
       SET is_deleted = true,
           deleted_at_utc = now(),
           deleted_reason = 'dedup: duplicate active widget_code'
     WHERE is_deleted = false
       AND EXISTS (
            SELECT 1 FROM public.dashboard_widget_definitions d2
            WHERE d2.widget_code = d.widget_code
              AND d2.is_deleted = false
              AND d2.id < d.id
       );

    CREATE UNIQUE INDEX IF NOT EXISTS ux_dashboard_widget_definitions_widget_code_active
        ON public.dashboard_widget_definitions (widget_code)
        WHERE is_deleted = false;
END
$widx$;