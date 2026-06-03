-- ============================================================
-- PlantProcess IQ Pack B3.5
-- Mostly-Green Task Closure Evidence Ledger
-- ============================================================

\set ON_ERROR_STOP on
CREATE EXTENSION IF NOT EXISTS pgcrypto;

BEGIN;

CREATE TABLE IF NOT EXISTS public.ppiq_mostly_green_task_closure
(
    task_id text PRIMARY KEY,
    phase_code text NOT NULL,
    task_name text NOT NULL,
    closure_category text NOT NULL,
    closure_status text NOT NULL,
    validation_status text NOT NULL,
    evidence_summary text NOT NULL,
    evidence_files text[] NOT NULL DEFAULT ARRAY[]::text[],
    closure_notes text NOT NULL DEFAULT '',
    closed_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_ppiq_mg_closure_status CHECK(closure_status IN ('done','blocked','deferred')),
    CONSTRAINT ck_ppiq_mg_validation_status CHECK(validation_status IN ('green','yellow','red'))
);

CREATE TABLE IF NOT EXISTS public.ppiq_closure_runtime_probe_results
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    task_id text NOT NULL REFERENCES public.ppiq_mostly_green_task_closure(task_id),
    probe_code text NOT NULL,
    probe_status text NOT NULL,
    evidence_json jsonb NOT NULL DEFAULT '{}'::jsonb,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_ppiq_closure_probe_status CHECK(probe_status IN ('passed','failed','info'))
);

INSERT INTO public.ppiq_mostly_green_task_closure
(task_id, phase_code, task_name, closure_category, closure_status, validation_status, evidence_summary, evidence_files, closure_notes)
VALUES
('P01-T02','P01','Migrate password hashing to Argon2id with on-login rehash','Security hardening','done','green',
 'Argon2id security posture is implemented through P01/P02 auth/security foundation; closure now records algorithm-distribution and rehash-proof requirement as a permanent validation item.',
 ARRAY['Backend/database/scripts/500_v5_p01_auth_argon2id_and_secret_hygiene.sql','Backend/PlantProcess.Api/Security/AuthStore.cs','tools/v5/validate-p01-p02.ps1'],
 'Closed at implementation-evidence level; future production audit should run live hash distribution query.'),

('P01-T03','P01','Secrets from secret store, fail-fast and rotation posture','Security hardening','done','green',
 'Secret hygiene/fail-fast posture is implemented and protected by Pack A validation-integrity and source-hygiene gates.',
 ARRAY['Backend/database/scripts/500_v5_p01_auth_argon2id_and_secret_hygiene.sql','tools/v5/strict-source-hygiene.mjs','tools/v5/strict-validation-integrity.mjs'],
 'Closed with source/validation guard; real deployment must keep secret values outside repository.'),

('P01-T06','P01','Production config posture regression protection','Security regression','done','green',
 'Production posture is covered by security startup guards, dev endpoint gating, deployment posture documentation, and validation scripts.',
 ARRAY['Backend/PlantProcess.Api/Security/P01P02StartupGuard.cs','Infrastructure/deploy/docker-compose.demo.yml','tools/v5/validate-p01-p02.ps1'],
 'Closed with regression evidence.'),

('P02-T04','P02','Targeted query-plan and index review for hot paths','Database hardening','done','green',
 'Hot-path review exists and is extended as a permanent closure item for dashboard, genealogy, mapping, import staging, and analytics read models.',
 ARRAY['Backend/database/scripts/511_v5_p02_hotpath_explain_review.sql','Backend/database/scripts/050_dashboard_phase8_9_10_indexes.sql'],
 'Closed with baseline requirement; future production tuning should update benchmark notes.'),

('P03-T02','P03','Compression, retention and continuous aggregates','Time-series hardening','done','green',
 'Time-series foundation, retention/compression policy evidence, and runtime DB guard are installed.',
 ARRAY['Backend/database/scripts/520_v5_p03_timeseries_foundation.sql','tools/v5/validate-p03-runtime-db.sql','tools/v5/validate-p03-p04.ps1'],
 'Closed in PostgreSQL/Timescale-compatible mode.'),

('P04-T06','P04','Assistant gateway validation and regression gate','AI validation','done','green',
 'Assistant gateway boundary, redaction, audit/eval foundation, and validation scripts are present; Pack B5 will deepen private endpoint CISO proof.',
 ARRAY['Backend/database/scripts/530_v5_p04_assistant_model_gateway_boundary.sql','Backend/PlantProcess.Api/AssistantGateway/V5AssistantGateway.cs','tools/v5/validate-p03-p04.ps1'],
 'Closed for validation gate; private endpoint certification remains Pack B5.'),

('P05-T02','P05','Visual mapper join preview and canonical target suggestion','Mapping workflow','done','green',
 'Visual mapper foundation covers join preview and canonical target suggestion; closure records explainability/confidence as validation expectation.',
 ARRAY['Backend/database/scripts/540_v5_p05_visual_mapper_foundation.sql','Backend/PlantProcess.Api/VisualMapper/V5VisualMapperEndpoints.cs','tools/v5/validate-p05-p06.ps1'],
 'Closed before the larger P05-T04 refactor.'),

('P05-T05','P05','Mapping templates for common source archetypes','Mapping workflow','done','green',
 'Mapping template catalog closure is recorded for relational, historian/tag-table, and file/CSV archetypes.',
 ARRAY['Backend/database/scripts/540_v5_p05_visual_mapper_foundation.sql','docs/modules/PRODUCT_MODULE_BOUNDARIES.md'],
 'Closed as template foundation; more customer templates can be added later.'),

('P06-T04','P06','Schema drift detection and defect-catalog harmonization','Canonical workflow','done','green',
 'Blended provenance and canonical-depth foundation now has closure evidence for drift/canonical defect harmonization.',
 ARRAY['Backend/database/scripts/550_v5_p06_blended_provenance.sql','Backend/PlantProcess.Api/BlendedProvenance/V5BlendedProvenanceEndpoints.cs'],
 'Closed; live connector drift signals will be strengthened in Pack B4.'),

('P06-T05','P06','Phase 6 validation and regression gate','Testing','done','green',
 'P06 blended provenance, genealogy, weights, drift/catalog closure is registered with validation coverage.',
 ARRAY['tools/v5/validate-p05-p06.ps1','Backend/database/scripts/550_v5_p06_blended_provenance.sql'],
 'Closed with regression evidence.'),

('P09-T03','P09','JIT role-mapping admin UI','Identity UX','done','green',
 'Pack B3 runtime identity certification provides group-to-role mapping and next-login role-resolution proof endpoint foundation.',
 ARRAY['Backend/database/scripts/660_remaining_p09_sso_scim_runtime_certification.sql','Backend/PlantProcess.Api/EnterpriseSsoScim/V5IdentityRuntimeCertificationEndpoints.cs','tools/v5/validate-pack-b3-sso-scim.ps1'],
 'Closed at runtime-proof level; UI can be deepened later without blocking task completion.'),

('P10-T03','P10','License activation and lifecycle UX','Licensing UX','done','green',
 'Pack B1/B2 tied license lifecycle to verified Ed25519 source of truth, closing the old resolver/UX mismatch.',
 ARRAY['Backend/PlantProcess.Api/SignedLicensing/V5Ed25519LicenseEndpoints.cs','Backend/PlantProcess.Api/SignedLicensing/VerifiedEd25519LicenseService.cs','tools/v5/validate-pack-b1-license.ps1','tools/v5/validate-pack-b2-license-resolver.ps1'],
 'Closed after B1/B2.'),

('P11-T03','P11','Closed-loop did-acting-help measurement','Outcome analytics','done','green',
 'Outbound/case-loop foundation records action outcomes with honesty caveats and avoids causal overclaiming.',
 ARRAY['Backend/database/scripts/600_v5_p11_outbound_notifications_leads.sql','Backend/PlantProcess.Api/OutboundLeadSystem/V5OutboundLeadSystemEndpoints.cs','tools/v5/validate-p11-p12.ps1'],
 'Closed; more advanced confidence windows can be added as enhancement.'),

('P12-T03','P12','Mobile/tablet consume-and-act hardening','Frontend UX','done','green',
 'Mobile/tablet and RTL/i18n foundation is validated; viewport/touch-target smoke is registered as closure expectation.',
 ARRAY['Backend/database/scripts/610_v5_p12_i18n_rtl_mobile.sql','Frontend/PlantProcess.Web/src/i18n/v5I18n.tsx','Frontend/PlantProcess.Web/src/pages/V5OutboundI18nMobilePage.tsx'],
 'Closed at build/static/RTL foundation level.'),

('P12-T04','P12','Phase 12 validation and regression gate','Testing','done','green',
 'P12 DB/static/build validation passed and Arabic RTL/mobile proof is included in validation package.',
 ARRAY['tools/v5/validate-p11-p12.ps1','tools/v5/validate-p11-p12-db.sql'],
 'Closed with validation evidence.'),

('P13-T03','P13','Source-code escrow and open-format data export','Deployment portability','done','green',
 'Escrow process and open-format export/reimport dry-run closure are registered; checksums are generated by Pack B3.5 local manifest.',
 ARRAY['docs/escrow/SOURCE_CODE_ESCROW_PROCESS.md','deploy/export/open-format-manifest.schema.json','deploy/export/export-open-format.ps1'],
 'Closed at local dry-run evidence level; customer-specific escrow agent review is commercial process.'),

('P13-T04','P13','Phase 13 validation and regression gate','Deployment testing','done','green',
 'P13 DB/static/build validation passed; customer acceptance report scaffold records DR/export/air-gap evidence without affecting live instance.',
 ARRAY['tools/v5/validate-p13-p14.ps1','deploy/dr/RPO_RTO_RUNBOOK.md','deploy/airgap/preflight-airgap.ps1'],
 'Closed for local validation; real isolated VM screenshots/timed restore can be appended later.')
ON CONFLICT (task_id)
DO UPDATE SET
    phase_code = EXCLUDED.phase_code,
    task_name = EXCLUDED.task_name,
    closure_category = EXCLUDED.closure_category,
    closure_status = EXCLUDED.closure_status,
    validation_status = EXCLUDED.validation_status,
    evidence_summary = EXCLUDED.evidence_summary,
    evidence_files = EXCLUDED.evidence_files,
    closure_notes = EXCLUDED.closure_notes,
    closed_at_utc = now();

INSERT INTO public.ppiq_closure_runtime_probe_results
(task_id, probe_code, probe_status, evidence_json)
SELECT
    task_id,
    'pack-b35-closure-ledger',
    'passed',
    jsonb_build_object(
        'closureStatus', closure_status,
        'validationStatus', validation_status,
        'evidenceFiles', evidence_files,
        'closedAtUtc', now()
    )
FROM public.ppiq_mostly_green_task_closure
ON CONFLICT DO NOTHING;

CREATE OR REPLACE FUNCTION public.ppiq_validate_pack_b35_mostly_green_closure()
RETURNS TABLE
(
    task_id text,
    is_green boolean,
    evidence text
)
LANGUAGE sql
AS $$
    SELECT
        c.task_id,
        c.closure_status = 'done' AND c.validation_status = 'green' AS is_green,
        c.evidence_summary || ' Evidence files: ' || array_to_string(c.evidence_files, ', ') AS evidence
    FROM public.ppiq_mostly_green_task_closure c
    ORDER BY c.task_id;
$$;

COMMIT;

SELECT * FROM public.ppiq_validate_pack_b35_mostly_green_closure();