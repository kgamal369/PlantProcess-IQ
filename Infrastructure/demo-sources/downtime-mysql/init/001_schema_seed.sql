CREATE TABLE IF NOT EXISTS equipment_stoppages (
  stoppage_id varchar(64) PRIMARY KEY,
  equipment_code varchar(64) NOT NULL,
  affected_material_code varchar(64) NOT NULL,
  started_at_utc datetime NOT NULL,
  ended_at_utc datetime NOT NULL,
  reason_code varchar(64) NOT NULL,
  duration_min decimal(10,3) NOT NULL
);

INSERT INTO equipment_stoppages VALUES
('ADV_DOWNTIME4002','HSM-F5','ADV_COIL4002','2026-05-01 11:04:00','2026-05-01 11:09:00','ROLL_FORCE_SPIKE',5.0)
ON DUPLICATE KEY UPDATE duration_min = VALUES(duration_min);
