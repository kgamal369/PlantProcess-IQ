CREATE TABLE IF NOT EXISTS heats (
  heat_id text PRIMARY KEY,
  plant_code text NOT NULL,
  furnace_code text NOT NULL,
  grade_code text NOT NULL,
  tap_start_utc timestamptz NOT NULL,
  tap_end_utc timestamptz NOT NULL,
  target_carbon_pct numeric(10,5),
  target_manganese_pct numeric(10,5)
);

CREATE TABLE IF NOT EXISTS heat_samples (
  sample_id text PRIMARY KEY,
  heat_id text NOT NULL REFERENCES heats(heat_id),
  sample_time_utc timestamptz NOT NULL,
  c_pct numeric(10,5),
  mn_pct numeric(10,5),
  si_pct numeric(10,5),
  p_pct numeric(10,5),
  s_pct numeric(10,5)
);

CREATE TABLE IF NOT EXISTS heat_additives (
  additive_id text PRIMARY KEY,
  heat_id text NOT NULL REFERENCES heats(heat_id),
  additive_code text NOT NULL,
  amount_kg numeric(14,3) NOT NULL,
  charged_at_utc timestamptz NOT NULL
);

INSERT INTO heats VALUES
('ADV_HEAT4002','DEMO_FLAT_STEEL','EAF-01','S355MC','2026-05-01T08:00:00Z','2026-05-01T08:47:00Z',0.06500,1.45000)
ON CONFLICT (heat_id) DO UPDATE SET grade_code = EXCLUDED.grade_code;

INSERT INTO heat_samples VALUES
('ADV_HEAT4002_S1','ADV_HEAT4002','2026-05-01T08:35:00Z',0.06200,1.42000,0.18000,0.01200,0.00600)
ON CONFLICT (sample_id) DO UPDATE SET c_pct = EXCLUDED.c_pct;

INSERT INTO heat_additives VALUES
('ADV_HEAT4002_ADD1','ADV_HEAT4002','FeMn',820.000,'2026-05-01T08:20:00Z')
ON CONFLICT (additive_id) DO UPDATE SET amount_kg = EXCLUDED.amount_kg;
