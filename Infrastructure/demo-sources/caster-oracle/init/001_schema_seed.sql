CREATE TABLE caster_sequences (
  sequence_id varchar2(64) PRIMARY KEY,
  heat_id varchar2(64) NOT NULL,
  ladle_id varchar2(64) NOT NULL,
  tundish_id varchar2(64) NOT NULL,
  strand_no number NOT NULL,
  cast_start_utc timestamp NOT NULL,
  cast_end_utc timestamp NOT NULL,
  superheat_c number(10,3)
);
/

CREATE TABLE caster_slabs (
  slab_id varchar2(64) PRIMARY KEY,
  sequence_id varchar2(64) NOT NULL,
  heat_id varchar2(64) NOT NULL,
  mould_id varchar2(64) NOT NULL,
  strand_no number NOT NULL,
  slab_weight_t number(10,3),
  slab_width_mm number(10,2),
  slab_thickness_mm number(10,2)
);
/

DECLARE
  v_heat_id varchar2(64);
  v_sequence_id varchar2(64);
  v_slab_id varchar2(64);
BEGIN
  FOR i IN 1..630 LOOP
    v_heat_id := 'H-' || LPAD(3300 + i, 4, '0');
    v_sequence_id := 'SEQ-' || LPAD(3300 + i, 4, '0');

    MERGE INTO caster_sequences t
    USING (SELECT v_sequence_id sequence_id FROM dual) s
    ON (t.sequence_id = s.sequence_id)
    WHEN NOT MATCHED THEN INSERT
      (sequence_id, heat_id, ladle_id, tundish_id, strand_no, cast_start_utc, cast_end_utc, superheat_c)
    VALUES
      (v_sequence_id, v_heat_id, 'LADLE-' || LPAD(3300 + i, 4, '0'), 'TUNDISH-' || LPAD(3300 + i, 4, '0'),
       MOD(i, 2) + 1, TIMESTAMP '2026-05-01 00:00:00' + NUMTODSINTERVAL(i * 67, 'MINUTE'),
       TIMESTAMP '2026-05-01 00:00:00' + NUMTODSINTERVAL(i * 67 + 61, 'MINUTE'),
       20 + MOD(i, 18));
  END LOOP;

  FOR i IN 1..5600 LOOP
    v_heat_id := 'H-' || LPAD(3300 + MOD(i - 1, 630) + 1, 4, '0');
    v_sequence_id := 'SEQ-' || LPAD(3300 + MOD(i - 1, 630) + 1, 4, '0');
    v_slab_id := 'S-' || LPAD(4400000 + i, 7, '0');

    MERGE INTO caster_slabs t
    USING (SELECT v_slab_id slab_id FROM dual) s
    ON (t.slab_id = s.slab_id)
    WHEN NOT MATCHED THEN INSERT
      (slab_id, sequence_id, heat_id, mould_id, strand_no, slab_weight_t, slab_width_mm, slab_thickness_mm)
    VALUES
      (v_slab_id, v_sequence_id, v_heat_id, 'MOULD-' || LPAD(MOD(i, 8) + 1, 2, '0'), MOD(i, 2) + 1,
       18 + MOD(i, 9), 1180 + MOD(i, 180), 220);
  END LOOP;

  MERGE INTO caster_sequences t
  USING (SELECT 'SEQ-3361' sequence_id FROM dual) s
  ON (t.sequence_id = s.sequence_id)
  WHEN NOT MATCHED THEN INSERT
    (sequence_id, heat_id, ladle_id, tundish_id, strand_no, cast_start_utc, cast_end_utc, superheat_c)
  VALUES
    ('SEQ-3361', 'H-3361', 'LADLE-3361', 'TUNDISH-3361', 2, TIMESTAMP '2026-05-14 09:05:00', TIMESTAMP '2026-05-14 10:10:00', 34.5);

  MERGE INTO caster_slabs t
  USING (SELECT 'S-0044170' slab_id FROM dual) s
  ON (t.slab_id = s.slab_id)
  WHEN NOT MATCHED THEN INSERT
    (slab_id, sequence_id, heat_id, mould_id, strand_no, slab_weight_t, slab_width_mm, slab_thickness_mm)
  VALUES
    ('S-0044170', 'SEQ-3361', 'H-3361', 'MOULD-02', 2, 22.4, 1250, 220);
END;
/