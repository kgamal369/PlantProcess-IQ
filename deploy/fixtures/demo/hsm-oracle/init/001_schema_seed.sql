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

DECLARE
  v_coil_id varchar2(64);
  v_slab_id varchar2(64);
BEGIN
  FOR i IN 1..5600 LOOP
    v_coil_id := 'C-' || LPAD(4400000 + i, 7, '0');
    v_slab_id := 'S-' || LPAD(4400000 + i, 7, '0');

    MERGE INTO hsm_coils t
    USING (SELECT v_coil_id coil_id FROM dual) s
    ON (t.coil_id = s.coil_id)
    WHEN NOT MATCHED THEN INSERT
      (coil_id, slab_id, hsm_campaign, product_code, rolling_start_utc, rolling_end_utc, target_fdt_c, actual_fdt_c, target_ct_c, actual_ct_c)
    VALUES
      (v_coil_id, v_slab_id, 'HSM-CAMP-2026-05', CASE WHEN MOD(i, 3) = 0 THEN 'S355MC_2.5x1250' ELSE 'DX51D_1.2x1000' END,
       TIMESTAMP '2026-05-01 00:00:00' + NUMTODSINTERVAL(i * 9, 'MINUTE'),
       TIMESTAMP '2026-05-01 00:00:00' + NUMTODSINTERVAL(i * 9 + 8, 'MINUTE'),
       880, 875 + MOD(i, 24), 620, 612 + MOD(i, 34));

    FOR s_no IN 1..7 LOOP
      MERGE INTO hsm_measurements t
      USING (SELECT 'M-' || v_coil_id || '-' || s_no measurement_id FROM dual) s
      ON (t.measurement_id = s.measurement_id)
      WHEN NOT MATCHED THEN INSERT
        (measurement_id, coil_id, measured_at_utc, stand_no, rolling_force_kn, flatness_iunit)
      VALUES
        ('M-' || v_coil_id || '-' || s_no, v_coil_id,
         TIMESTAMP '2026-05-01 00:00:00' + NUMTODSINTERVAL(i * 9 + s_no, 'MINUTE'),
         s_no, 15000 + MOD(i * s_no, 900), MOD(i * s_no, 45));
    END LOOP;
  END LOOP;

  MERGE INTO hsm_coils t
  USING (SELECT 'C-0044170' coil_id FROM dual) s
  ON (t.coil_id = s.coil_id)
  WHEN NOT MATCHED THEN INSERT
    (coil_id, slab_id, hsm_campaign, product_code, rolling_start_utc, rolling_end_utc, target_fdt_c, actual_fdt_c, target_ct_c, actual_ct_c)
  VALUES
    ('C-0044170', 'S-0044170', 'HSM-CAMP-2026-05', 'S355MC_2.5x1250',
     TIMESTAMP '2026-05-14 11:00:00', TIMESTAMP '2026-05-14 11:08:00', 880, 898, 620, 641);

  MERGE INTO hsm_coils t
  USING (SELECT 'C-ORPHAN-0001' coil_id FROM dual) s
  ON (t.coil_id = s.coil_id)
  WHEN NOT MATCHED THEN INSERT
    (coil_id, slab_id, hsm_campaign, product_code, rolling_start_utc, rolling_end_utc, target_fdt_c, actual_fdt_c, target_ct_c, actual_ct_c)
  VALUES
    ('C-ORPHAN-0001', 'S-MISSING-0001', 'HSM-CAMP-2026-05', 'UNKNOWN_ORPHAN',
     TIMESTAMP '2026-05-15 11:00:00', TIMESTAMP '2026-05-15 11:08:00', 880, 880, 620, 620);
END;
/