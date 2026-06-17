-- 000_schemas.sql  (000 prefix = ordering token, runs before all numbered migrations)
-- CREATE SCHEMA is not CREATE TABLE, so ordered SQL is permitted; table placement stays model-first.
CREATE SCHEMA IF NOT EXISTS ppiq_meta;
COMMENT ON SCHEMA ppiq_meta IS 'App metadata: dashboards, widgets, jobs, pages, users, roles, license.';
CREATE SCHEMA IF NOT EXISTS ppiq_plant;
COMMENT ON SCHEMA ppiq_plant IS 'Customer plant data after staging transform; RLS per tenant.';