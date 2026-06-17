CREATE TABLE IF NOT EXISTS meltshop_heats
(
    heat_id text PRIMARY KEY,
    furnace_no integer NOT NULL,
    tap_start_utc timestamptz NOT NULL,
    tap_end_utc timestamptz NOT NULL,
    steel_grade text NOT NULL,
    target_temp_c numeric(10,3),
    tap_temp_c numeric(10,3),
    carbon_pct numeric(10,5),
    oxygen_ppm numeric(10,3)
);

INSERT INTO meltshop_heats
(
    heat_id,
    furnace_no,
    tap_start_utc,
    tap_end_utc,
    steel_grade,
    target_temp_c,
    tap_temp_c,
    carbon_pct,
    oxygen_ppm
)
SELECT
    'H-' || lpad((3300 + g)::text, 4, '0') AS heat_id,
    1 + (g % 2) AS furnace_no,
    timestamptz '2026-05-01 00:00:00+00' + make_interval(mins => g * 67),
    timestamptz '2026-05-01 00:00:00+00' + make_interval(mins => g * 67 + 52),
    CASE WHEN g % 3 = 0 THEN 'S355MC' WHEN g % 3 = 1 THEN 'DX51D' ELSE 'S420MC' END,
    1605 + (g % 9),
    1603 + (g % 15),
    0.045 + ((g % 11)::numeric / 10000.0),
    350 + (g % 90)
FROM generate_series(1, 630) AS g
ON CONFLICT (heat_id)
DO UPDATE SET
    furnace_no = EXCLUDED.furnace_no,
    tap_start_utc = EXCLUDED.tap_start_utc,
    tap_end_utc = EXCLUDED.tap_end_utc,
    steel_grade = EXCLUDED.steel_grade,
    target_temp_c = EXCLUDED.target_temp_c,
    tap_temp_c = EXCLUDED.tap_temp_c,
    carbon_pct = EXCLUDED.carbon_pct,
    oxygen_ppm = EXCLUDED.oxygen_ppm;

INSERT INTO meltshop_heats
(
    heat_id,
    furnace_no,
    tap_start_utc,
    tap_end_utc,
    steel_grade,
    target_temp_c,
    tap_temp_c,
    carbon_pct,
    oxygen_ppm
)
VALUES
(
    'H-3361',
    2,
    '2026-05-14T08:00:00Z',
    '2026-05-14T08:52:00Z',
    'S355MC',
    1608,
    1629,
    0.05210,
    418
)
ON CONFLICT (heat_id)
DO UPDATE SET
    furnace_no = EXCLUDED.furnace_no,
    tap_start_utc = EXCLUDED.tap_start_utc,
    tap_end_utc = EXCLUDED.tap_end_utc,
    steel_grade = EXCLUDED.steel_grade,
    target_temp_c = EXCLUDED.target_temp_c,
    tap_temp_c = EXCLUDED.tap_temp_c,
    carbon_pct = EXCLUDED.carbon_pct,
    oxygen_ppm = EXCLUDED.oxygen_ppm;