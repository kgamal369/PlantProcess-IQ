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

MERGE INTO caster_sequences t USING (SELECT 'ADV_SEQ4002' sequence_id FROM dual) s ON (t.sequence_id=s.sequence_id)
WHEN NOT MATCHED THEN INSERT (sequence_id,heat_id,ladle_id,tundish_id,strand_no,cast_start_utc,cast_end_utc,superheat_c)
VALUES ('ADV_SEQ4002','ADV_HEAT4002','ADV_LADLE4002','ADV_TUNDISH4002',2,TIMESTAMP '2026-05-01 09:05:00',TIMESTAMP '2026-05-01 10:10:00',27.5);
/

MERGE INTO caster_slabs t USING (SELECT 'ADV_SLAB4002' slab_id FROM dual) s ON (t.slab_id=s.slab_id)
WHEN NOT MATCHED THEN INSERT (slab_id,sequence_id,heat_id,mould_id,strand_no,slab_weight_t,slab_width_mm,slab_thickness_mm)
VALUES ('ADV_SLAB4002','ADV_SEQ4002','ADV_HEAT4002','MOLD-02',2,22.8,1260,230);
/
