CREATE TABLE IF NOT EXISTS surface_defects (
  defect_id varchar(64) PRIMARY KEY,
  coil_id varchar(64) NOT NULL,
  inspected_at_utc datetime NOT NULL,
  defect_code varchar(64) NOT NULL,
  defect_class varchar(64) NOT NULL,
  position_m decimal(12,3) NOT NULL,
  severity varchar(32) NOT NULL
);

INSERT INTO surface_defects VALUES
('ADV_DEFECT4002_1','ADV_COIL4002','2026-05-01 15:10:00','SCRATCH_LONG','Surface',428.5,'Medium')
ON DUPLICATE KEY UPDATE severity = VALUES(severity);
