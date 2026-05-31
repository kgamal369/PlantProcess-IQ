const fs = require("node:fs");

const file = "Backend/database/scripts/205_phase04_phase05_completion_governance_jobs_tests.sql";
let text = fs.readFileSync(file, "utf8");

const replacements = [
  [
    `FROM public.job_definitions
    WHERE job_code = v_target_job_code`,
    `FROM public.job_definitions
    WHERE public.job_definitions.job_code = v_target_job_code`
  ],
  [
    `FROM public.job_definitions
    WHERE job_code IN`,
    `FROM public.job_definitions
    WHERE public.job_definitions.job_code IN`
  ],
  [
    `WHERE job_code IN
    (
        'SYSTEM_ML_PARAMS_VS_DEFECTS'`,
    `WHERE public.job_definitions.job_code IN
    (
        'SYSTEM_ML_PARAMS_VS_DEFECTS'`
  ]
];

let changed = 0;

for (const [before, after] of replacements) {
  if (text.includes(before)) {
    text = text.replace(before, after);
    changed++;
  }
}

if (changed === 0) {
  throw new Error("Could not find unqualified job_code references to patch.");
}

fs.writeFileSync(file, text, "utf8");

console.log(`Patched ${changed} unqualified job_code reference(s) in SQL 205.`);
