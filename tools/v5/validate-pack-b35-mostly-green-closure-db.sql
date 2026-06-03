\encoding UTF8
SET client_encoding = 'UTF8';

SELECT * FROM public.ppiq_validate_pack_b35_mostly_green_closure();

DO $$
DECLARE
    bad_count integer;
    task_count integer;
BEGIN
    SELECT count(*) INTO task_count
    FROM public.ppiq_validate_pack_b35_mostly_green_closure();

    IF task_count <> 17 THEN
        RAISE EXCEPTION 'Expected 17 mostly-green closure tasks, got %.', task_count;
    END IF;

    SELECT count(*) INTO bad_count
    FROM public.ppiq_validate_pack_b35_mostly_green_closure()
    WHERE is_green = false;

    IF bad_count > 0 THEN
        RAISE EXCEPTION 'Pack B3.5 closure has % failing task(s).', bad_count;
    END IF;
END $$;