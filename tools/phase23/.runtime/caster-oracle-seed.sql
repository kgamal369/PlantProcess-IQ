SET DEFINE OFF
SET HEADING OFF
SET FEEDBACK OFF
SET VERIFY OFF
SET ECHO OFF
SET PAGESIZE 0
SET LINESIZE 200
WHENEVER SQLERROR EXIT SQL.SQLCODE

BEGIN
    EXECUTE IMMEDIATE '
        CREATE TABLE caster_sequences (
            sequence_id varchar2(64) PRIMARY KEY,
            heat_id varchar2(64) NOT NULL,
            ladle_id varchar2(64) NOT NULL,
            tundish_id varchar2(64) NOT NULL,
            strand_no number NOT NULL,
            cast_start_utc timestamp NOT NULL,
            cast_end_utc timestamp NOT NULL,
            superheat_c number(10,3)
        )';
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE != -955 THEN
            RAISE;
        END IF;
END;
/

BEGIN
    EXECUTE IMMEDIATE '
        CREATE TABLE caster_slabs (
            slab_id varchar2(64) PRIMARY KEY,
            sequence_id varchar2(64) NOT NULL,
            heat_id varchar2(64) NOT NULL,
            mould_id varchar2(64) NOT NULL,
            strand_no number NOT NULL,
            slab_weight_t number(10,3),
            slab_width_mm number(10,2),
            slab_thickness_mm number(10,2)
        )';
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE != -955 THEN
            RAISE;
        END IF;
END;
/

MERGE INTO caster_sequences t
USING (SELECT 'ADV_SEQ4002' sequence_id FROM dual) s
ON (t.sequence_id = s.sequence_id)
WHEN MATCHED THEN UPDATE SET
    heat_id = 'ADV_HEAT4002',
    ladle_id = 'ADV_LADLE4002',
    tundish_id = 'ADV_TUNDISH4002',
    strand_no = 2,
    cast_start_utc = TIMESTAMP '2026-05-01 09:05:00',
    cast_end_utc = TIMESTAMP '2026-05-01 10:10:00',
    superheat_c = 27.5
WHEN NOT MATCHED THEN INSERT
    (sequence_id, heat_id, ladle_id, tundish_id, strand_no, cast_start_utc, cast_end_utc, superheat_c)
VALUES
    ('ADV_SEQ4002', 'ADV_HEAT4002', 'ADV_LADLE4002', 'ADV_TUNDISH4002', 2, TIMESTAMP '2026-05-01 09:05:00', TIMESTAMP '2026-05-01 10:10:00', 27.5);

MERGE INTO caster_slabs t
USING (SELECT 'ADV_SLAB4002' slab_id FROM dual) s
ON (t.slab_id = s.slab_id)
WHEN MATCHED THEN UPDATE SET
    sequence_id = 'ADV_SEQ4002',
    heat_id = 'ADV_HEAT4002',
    mould_id = 'MOLD-02',
    strand_no = 2,
    slab_weight_t = 22.8,
    slab_width_mm = 1260,
    slab_thickness_mm = 230
WHEN NOT MATCHED THEN INSERT
    (slab_id, sequence_id, heat_id, mould_id, strand_no, slab_weight_t, slab_width_mm, slab_thickness_mm)
VALUES
    ('ADV_SLAB4002', 'ADV_SEQ4002', 'ADV_HEAT4002', 'MOLD-02', 2, 22.8, 1260, 230);

COMMIT;

SELECT COUNT(*) FROM caster_sequences WHERE sequence_id = 'ADV_SEQ4002';

EXIT;
