CREATE TABLE hsm_coils (
  coil_id varchar2(64) PRIMARY KEY,
  slab_id varchar2(64) NOT NULL,
  hsm_campaign varchar2(64) NOT NULL,
  product_code varchar2(64) NOT NULL,
  rolling_start_utc timestamp NOT NULL,
  rolling_end_utc timestamp NOT NULL,
  target_fdt_c number(10,3),
  actual_fdt_c number(10,3),
  target_ct_c number(10,3),
  actual_ct_c number(10,3)
);
/

CREATE TABLE hsm_measurements (
  measurement_id varchar2(64) PRIMARY KEY,
  coil_id varchar2(64) NOT NULL,
  measured_at_utc timestamp NOT NULL,
  stand_no number NOT NULL,
  rolling_force_kn number(12,3),
  flatness_iunit number(12,3)
);
/

MERGE INTO hsm_coils t USING (SELECT 'ADV_COIL4002' coil_id FROM dual) s ON (t.coil_id=s.coil_id)
WHEN NOT MATCHED THEN INSERT (coil_id,slab_id,hsm_campaign,product_code,rolling_start_utc,rolling_end_utc,target_fdt_c,actual_fdt_c,target_ct_c,actual_ct_c)
VALUES ('ADV_COIL4002','ADV_SLAB4002','HSM-CAMP-2026-05','S355MC_2.5x1250',TIMESTAMP '2026-05-01 11:00:00',TIMESTAMP '2026-05-01 11:08:00',880,894,620,638);
/

MERGE INTO hsm_measurements t USING (SELECT 'ADV_COIL4002_M1' measurement_id FROM dual) s ON (t.measurement_id=s.measurement_id)
WHEN NOT MATCHED THEN INSERT (measurement_id,coil_id,measured_at_utc,stand_no,rolling_force_kn,flatness_iunit)
VALUES ('ADV_COIL4002_M1','ADV_COIL4002',TIMESTAMP '2026-05-01 11:03:00',5,18450,18.2);
/
