CREATE TABLE IF NOT EXISTS parsytec_surface_defects
(
    defect_id varchar(64) PRIMARY KEY,
    coil_id varchar(64) NOT NULL,
    detected_at_utc datetime NOT NULL,
    camera_id varchar(64) NOT NULL,
    defect_code varchar(64) NOT NULL,
    severity varchar(32) NOT NULL,
    position_start_m decimal(12,3) NULL,
    position_end_m decimal(12,3) NULL,
    width_mm decimal(12,3) NULL
);

INSERT INTO parsytec_surface_defects
(
    defect_id,
    coil_id,
    detected_at_utc,
    camera_id,
    defect_code,
    severity,
    position_start_m,
    position_end_m,
    width_mm
)
WITH RECURSIVE n AS
(
    SELECT 1 AS i
    UNION ALL
    SELECT i + 1 FROM n WHERE i < 5600
)
SELECT
    CONCAT('D-', LPAD(4400000 + i, 7, '0')),
    CONCAT('C-', LPAD(4400000 + i, 7, '0')),
    DATE_ADD('2026-05-01 00:00:00', INTERVAL i * 9 MINUTE),
    CONCAT('CAM-', 1 + MOD(i, 6)),
    CASE WHEN MOD(i, 17) = 0 THEN 'EDGE_CRACK' WHEN MOD(i, 31) = 0 THEN 'SCALE' ELSE 'NONE' END,
    CASE WHEN MOD(i, 17) = 0 THEN 'High' WHEN MOD(i, 31) = 0 THEN 'Medium' ELSE 'Info' END,
    MOD(i * 3, 950),
    MOD(i * 3, 950) + 1.5,
    CASE WHEN MOD(i, 17) = 0 THEN 12.5 ELSE 0.0 END
FROM n
WHERE MOD(i, 17) = 0 OR MOD(i, 31) = 0
ON DUPLICATE KEY UPDATE
    coil_id = VALUES(coil_id),
    defect_code = VALUES(defect_code),
    severity = VALUES(severity);

INSERT INTO parsytec_surface_defects
(
    defect_id,
    coil_id,
    detected_at_utc,
    camera_id,
    defect_code,
    severity,
    position_start_m,
    position_end_m,
    width_mm
)
VALUES
(
    'D-C-0044170-EDGE',
    'C-0044170',
    '2026-05-14 11:18:00',
    'CAM-EDGE-02',
    'EDGE_CRACK',
    'High',
    417.0,
    421.0,
    18.5
),
(
    'D-C-0044170-UNKNOWN',
    'C-0044170',
    '2026-05-14 11:19:00',
    'CAM-EDGE-02',
    'UNMAPPED_DEMO_DEFECT',
    'Medium',
    500.0,
    500.5,
    4.0
)
ON DUPLICATE KEY UPDATE
    defect_code = VALUES(defect_code),
    severity = VALUES(severity);