const fs = require("node:fs");

const file = "Backend/database/scripts/205_phase04_phase05_completion_governance_jobs_tests.sql";
let text = fs.readFileSync(file, "utf8");

const marker = `BEGIN
    INSERT INTO public.job_definitions`;

const cleanup = `BEGIN
    -- Clean legacy duplicate ML job definitions created by earlier iterations.
    -- Keep only the canonical V7 codes:
    --   SYSTEM_ML_PARAMS_VS_DEFECTS
    --   SYSTEM_ML_PARAMS_VS_DOWNTIME
    --   SYSTEM_ML_PARAMS_VS_KPI
    --   SYSTEM_ML_WEEKLY_OVERALL
    UPDATE public.job_definitions
    SET is_deleted = TRUE,
        deleted_at_utc = now(),
        deleted_reason = 'Superseded by canonical V7 Phase 4/5 ML job definition',
        updated_at_utc = now()
    WHERE job_code IN
    (
        'SYSTEM_ML_PARAMS_VS_KPIS',
        'SYSTEM_ML_WEEKLY_FULL'
    )
      AND is_deleted = FALSE;

    INSERT INTO public.job_definitions`;

if (text.includes("Superseded by canonical V7 Phase 4/5 ML job definition")) {
  console.log("Legacy ML job cleanup already exists in SQL 205.");
} else {
  if (!text.includes(marker)) {
    throw new Error("Could not find ppiq_ml_ensure_job_definitions_v1 BEGIN marker.");
  }

  text = text.replace(marker, cleanup);
  fs.writeFileSync(file, text, "utf8");
  console.log("Patched SQL 205 to soft-delete legacy duplicate ML job definitions.");
}
