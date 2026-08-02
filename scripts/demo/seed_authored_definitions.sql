-- seed_authored_definitions.sql
--
-- PRESENTATION DATA, per T-006. Mapping versions and source dataset
-- definitions authored after the 13-Jul fixture was taken. Captured as row
-- JSON and replayed through json_populate_recordset, so this step is
-- column-agnostic and survives a schema change.
--
-- Idempotent: ON CONFLICT DO NOTHING.

-- ppiq_mapping_versions: 5 row(s)
INSERT INTO ppiq_mapping_versions
SELECT * FROM json_populate_recordset(NULL::ppiq_mapping_versions, '[{"id":"2d24bfd2-4573-4bc5-ab7d-4ad5a3596cae","tenant_id":"00000000-0000-0000-0000-000000000001","mapping_code":"P03_COMPLETION_PROOF_8c347d87","display_name":"P03/P04 previous published proof version","canonical_entity":"QualityEvent","environment":"demo","version_number":1,"definition":{"source": "public.v_ppiq_p03_coil_quality_event", "requiredFields": ["material_key", "event_time_utc", "quality_event_type"]},"status":"Published","created_at_utc":"2026-07-25T21:39:33.957179+03:00"},'::json)
ON CONFLICT (id) DO NOTHING;

-- source_dataset_definitions: 1 row(s)
INSERT INTO source_dataset_definitions
SELECT * FROM json_populate_recordset(NULL::source_dataset_definitions, '[{"id":"af97f5fd-c33a-4392-b8c1-ab5a0a941c8d","connection_profile_id":"dddd0000-0000-0000-0000-000000000206","dataset_code":"PPIQ_SRC_CC_HEATS","dataset_name":"CC_HEATS","dataset_kind":"SqlTable","next_run_at_utc":null,"source_object_name":"CC_HEATS","source_schema_name":"PPIQ_SRC","primary_timestamp_field":"START_UTC","incremental_cursor_field":"START_UTC","last_cursor_value":null,"refresh_interval_seconds":300,"dataset_options_json":{"rowFilter": null, "selectedColumns": null, "primaryKeyColumns": ["HEAT_ID", "SEQ_ID", "LADLE_ID", "TUNDISH_ID", "MOULD_ID", "CREW_ID"]},"is_active":true,"description":null,"created_at_utc":"2026-07-25T22:08:52.353561+03:00","updated_at_utc":"2026-07-25T22:08:52.353976+03:00","is_synthetic":false,"source_system":null,"source_record_id":null,"is_deleted":false,"deleted_at_utc":null,"deleted_reason":null}]'::json)
ON CONFLICT (id) DO NOTHING;
