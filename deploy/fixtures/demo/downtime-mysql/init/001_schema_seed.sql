CREATE TABLE IF NOT EXISTS downtime_events
(
    event_id varchar(64) PRIMARY KEY,
    equipment_code varchar(64) NOT NULL,
    started_at_utc datetime NOT NULL,
    ended_at_utc datetime NULL,
    reason_code varchar(64) NOT NULL,
    reason_text varchar(255) NOT NULL
);

INSERT INTO downtime_events
(
    event_id,
    equipment_code,
    started_at_utc,
    ended_at_utc,
    reason_code,
    reason_text
)
VALUES
('DT-HSM-20260514-001','HSM-FM','2026-05-14 10:42:00','2026-05-14 10:51:00','ROLL_CHANGE','Short roll-change delay before C-0044170'),
('DT-CASTER-20260514-001','CASTER-2','2026-05-14 09:58:00','2026-05-14 10:03:00','NOZZLE_CHECK','Nozzle stability check during H-3361'),
('DT-PKL-20260514-001','PKL-1','2026-05-14 14:22:00','2026-05-14 14:26:00','SPEED_REDUCTION','Line-speed reduction during pickling')
ON DUPLICATE KEY UPDATE
    equipment_code = VALUES(equipment_code),
    started_at_utc = VALUES(started_at_utc),
    ended_at_utc = VALUES(ended_at_utc),
    reason_code = VALUES(reason_code),
    reason_text = VALUES(reason_text);