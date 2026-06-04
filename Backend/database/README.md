# PlantProcess IQ Database Folder

This folder contains database scripts, seed data, views, source-system demo data, validation checks, and security/admin SQL.

## Current Stabilization Rule

Do not run every SQL file automatically.
Do not mix generic product schema scripts with demo/source-system data.
Do not run security/admin scripts inside the normal application migration flow.
Do not hard-code flat-steel/demo source-system SQL into the generic PlantProcess IQ core schema.

## Apply Modes

- AUTO_APPLY_CANDIDATE: candidate for normal app DB deployment after idempotency review.
- AUTO_APPLY_AFTER_SCHEMA: views/read-models that should run after base schema.
- ADMIN_ONLY: DBA/operator scripts only.
- OPTIONAL_SEED: optional seed data, not always required.
- OPTIONAL_DEMO_ONLY: demo/source-system data only.
- VALIDATION_ONLY: audit/performance/readiness checks only.
- DO_NOT_AUTO_APPLY: manual review required.
- DO_NOT_EXECUTE: non-SQL or reference material.

## Generated Files

- database.apply-order.manifest.csv
- database.apply-order.manifest.md

## Future Target Structure

Backend/database/migrations
Backend/database/security-admin
Backend/database/demo
Backend/database/demo/source-shapes
Backend/database/views
Backend/database/validation
Backend/database/archive
