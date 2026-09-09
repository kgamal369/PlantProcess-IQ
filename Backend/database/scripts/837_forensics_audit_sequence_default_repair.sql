-- 837_forensics_audit_sequence_default_repair.sql
--
-- Forward repair of the forensic audit id contract.
--
-- 750_forensics_audit_subsystem.sql was generated from a live catalog and the
-- capture lost the owned sequences and the id defaults for three audit tables.
-- What it kept was bigint NOT NULL, the primary keys, and the two insert
-- functions that never supply id. The result is that ppiq_forensics.audit_ddl()
-- cannot record anything: the first canonical DROP of a public table raises
-- instead of being audited. The subsystem whose purpose is recording
-- destructive DDL fails closed on the DDL it exists to record.
--
-- 750 is historical migration authority with a recorded hash and is not edited.
-- This is the forward repair, and it restores the intended physical model -
-- bigint id, owned sequence, DEFAULT nextval - rather than converting the
-- columns to identity, which would be a different design.
--
-- It runs before 836 by canonical position, not by filename ordinal, because
-- 836 performs the first canonical post-forensics DROP.
--
-- Idempotent, data preserving and replayable. An existing correctly owned
-- sequence is reused and advanced, never replaced. A sequence of the canonical
-- name already owned by something else fails closed rather than being stolen.
--
-- Targets are post-topology placements: topology convergence owns where these
-- tables live, and by this position it has already run.

BEGIN;

DO $repair$
DECLARE
    t              record;
    v_relid        oid;
    v_attnum       smallint;
    v_typname      text;
    v_notnull      boolean;
    v_serial       text;
    v_seq_qual     text;
    v_existing     oid;
    v_foreign      int;
    v_max          bigint;
    v_last         bigint;
    v_called       boolean;
    v_default      text;
    v_pk           int;
BEGIN
    FOR t IN
        SELECT *
        FROM (VALUES
            ('ppiq_forensics', 'wipe_audit',          'wipe_audit_id_seq'),
            ('ppiq_meta',      'ppiq_catalog_audit',  'ppiq_catalog_audit_id_seq'),
            ('ppiq_meta',      'ppiq_purge_audit',    'ppiq_purge_audit_id_seq')
        ) AS v(sch, tbl, seq)
    LOOP
        -- 1. the table exists at its post-topology placement
        v_relid := to_regclass(format('%I.%I', t.sch, t.tbl));
        IF v_relid IS NULL THEN
            RAISE EXCEPTION 'PPIQ_FORENSICS_REPAIR_TABLE_MISSING: %.% does not exist. Topology convergence owns its placement and must have run before this repair.', t.sch, t.tbl;
        END IF;

        -- 2-4. the id column exists, is bigint, and is NOT NULL
        SELECT a.attnum, format_type(a.atttypid, NULL), a.attnotnull
          INTO v_attnum, v_typname, v_notnull
          FROM pg_attribute a
         WHERE a.attrelid = v_relid AND a.attname = 'id' AND a.attnum > 0 AND NOT a.attisdropped;

        IF v_attnum IS NULL THEN
            RAISE EXCEPTION 'PPIQ_FORENSICS_REPAIR_ID_SHAPE: %.% has no id column.', t.sch, t.tbl;
        END IF;
        IF v_typname <> 'bigint' THEN
            RAISE EXCEPTION 'PPIQ_FORENSICS_REPAIR_ID_SHAPE: %.%.id is % and not bigint. This repair restores a sequence contract and will not reshape a column.', t.sch, t.tbl, v_typname;
        END IF;
        IF NOT v_notnull THEN
            RAISE EXCEPTION 'PPIQ_FORENSICS_REPAIR_ID_SHAPE: %.%.id is nullable, which is not the shape 750 declared.', t.sch, t.tbl;
        END IF;

        -- 5-6. reuse a correctly owned sequence if one is already bound
        v_serial := pg_get_serial_sequence(format('%I.%I', t.sch, t.tbl), 'id');

        IF v_serial IS NOT NULL THEN
            v_seq_qual := v_serial;
            RAISE NOTICE 'PPIQ_FORENSICS_REPAIR_SEQUENCE: %.%.id already owns %; reusing it.', t.sch, t.tbl, v_seq_qual;
        ELSE
            -- 7. no bound sequence. Adopt the canonical name only if it is free
            --    or already ours; never take one that belongs to another column.
            v_existing := to_regclass(format('%I.%I', t.sch, t.seq));
            IF v_existing IS NOT NULL THEN
                SELECT count(*)
                  INTO v_foreign
                  FROM pg_depend d
                 WHERE d.classid = 'pg_class'::regclass
                   AND d.objid = v_existing
                   AND d.deptype = 'a'
                   AND NOT (d.refobjid = v_relid AND d.refobjsubid = v_attnum);
                IF v_foreign > 0 THEN
                    RAISE EXCEPTION 'PPIQ_FORENSICS_REPAIR_SEQUENCE: %.% already exists and is owned by another object. Refusing to take it.', t.sch, t.seq;
                END IF;
            ELSE
                EXECUTE format('CREATE SEQUENCE %I.%I AS bigint', t.sch, t.seq);
                RAISE NOTICE 'PPIQ_FORENSICS_REPAIR_SEQUENCE: created %.%', t.sch, t.seq;
            END IF;

            -- 8-9. bind ownership, then the default
            EXECUTE format('ALTER SEQUENCE %I.%I OWNED BY %I.%I.id', t.sch, t.seq, t.sch, t.tbl);
            EXECUTE format('ALTER TABLE %I.%I ALTER COLUMN id SET DEFAULT nextval(%L::regclass)',
                           t.sch, t.tbl, format('%I.%I', t.sch, t.seq));
            v_seq_qual := format('%I.%I', t.sch, t.seq);
        END IF;

        -- 10. never hand out an id that already exists, and never rewind a
        --     populated audit sequence.
        EXECUTE format('SELECT coalesce(max(id), 0) FROM %I.%I', t.sch, t.tbl) INTO v_max;
        EXECUTE format('SELECT last_value, is_called FROM %s', v_seq_qual) INTO v_last, v_called;

        IF v_max = 0 THEN
            IF NOT v_called AND v_last <= 1 THEN
                PERFORM setval(v_seq_qual::regclass, 1, false);
            END IF;
        ELSE
            IF (CASE WHEN v_called THEN v_last + 1 ELSE v_last END) <= v_max THEN
                PERFORM setval(v_seq_qual::regclass, v_max, true);
                RAISE NOTICE 'PPIQ_FORENSICS_REPAIR_SEQUENCE: % advanced past max(id)=%', v_seq_qual, v_max;
            END IF;
        END IF;

        -- 11. postconditions, proven from the catalog rather than assumed
        SELECT pg_get_expr(ad.adbin, ad.adrelid)
          INTO v_default
          FROM pg_attrdef ad
         WHERE ad.adrelid = v_relid AND ad.adnum = v_attnum;

        IF v_default IS NULL OR position('nextval' in v_default) = 0 THEN
            RAISE EXCEPTION 'PPIQ_FORENSICS_REPAIR_DEFAULT: %.%.id has no sequence-backed default after repair.', t.sch, t.tbl;
        END IF;
        IF pg_get_serial_sequence(format('%I.%I', t.sch, t.tbl), 'id') IS NULL THEN
            RAISE EXCEPTION 'PPIQ_FORENSICS_REPAIR_SEQUENCE: %.%.id has a default but owns no sequence; ownership did not take.', t.sch, t.tbl;
        END IF;

        SELECT count(*) INTO v_pk FROM pg_constraint WHERE conrelid = v_relid AND contype = 'p';
        IF v_pk <> 1 THEN
            RAISE EXCEPTION 'PPIQ_FORENSICS_REPAIR_ID_SHAPE: %.% lost its primary key.', t.sch, t.tbl;
        END IF;

        RAISE NOTICE 'PPIQ_FORENSICS_REPAIR_PROVEN: %.%.id -> % (max(id)=%)', t.sch, t.tbl, v_default, v_max;
    END LOOP;

    RAISE NOTICE 'PPIQ_FORENSICS_REPAIR_PROVEN: all three audit tables carry an owned sequence and a functional default.';
END
$repair$;

COMMIT;