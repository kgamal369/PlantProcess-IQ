const fs = require("node:fs");

const file = "Backend/database/scripts/204_phase04_phase05_ml_learning_core.sql";
let text = fs.readFileSync(file, "utf8");

const beforeNumericPValue =
  "LEAST(1.0, exp(-abs(COALESCE(coefficient, 0)) * sqrt(sample_size::double precision))),";

const afterNumericPValue =
  "GREATEST(1e-300::double precision, LEAST(1.0::double precision, exp(GREATEST(-700.0::double precision, -abs(COALESCE(coefficient, 0)) * sqrt(sample_size::double precision))))),";

if (!text.includes(beforeNumericPValue)) {
  throw new Error("Could not find numeric p-value expression to patch.");
}

text = text.replace(beforeNumericPValue, afterNumericPValue);

const beforeCategoricalPValue =
  "LEAST(1.0, exp(-abs(spread) * sqrt(sample_size::double precision))),";

const afterCategoricalPValue =
  "GREATEST(1e-300::double precision, LEAST(1.0::double precision, exp(GREATEST(-700.0::double precision, -abs(spread) * sqrt(sample_size::double precision))))),";

if (!text.includes(beforeCategoricalPValue)) {
  throw new Error("Could not find categorical p-value expression to patch.");
}

text = text.replace(beforeCategoricalPValue, afterCategoricalPValue);

const groupedStart = text.indexOf(`    grouped AS
    (
        SELECT
            f.feature_key,`);

const groupedEnd = text.indexOf(`    )
    INSERT INTO public.ml_learning_results_v1`, groupedStart);

if (groupedStart < 0 || groupedEnd < 0) {
  throw new Error("Could not find categorical grouped CTE block.");
}

const groupedReplacement = `    grouped AS
    (
        SELECT
            feature_key,
            feature_grain,
            outcome_key,
            family,
            sum(n)::integer AS sample_size,
            count(DISTINCT heat_id)::integer AS effective_n,
            max(avg_y) - min(avg_y) AS spread
        FROM
        (
            SELECT
                f.feature_key,
                f.feature_grain,
                f.feature_value,
                o.outcome_key,
                o.family,
                f.heat_id,
                avg(o.y) AS avg_y,
                count(*)::integer AS n
            FROM cat_features f
            JOIN cat_outcomes o
              ON o.coil_id = f.coil_id
            WHERE v_family IN ('all', 'overall') OR o.family = v_family
            GROUP BY
                f.feature_key,
                f.feature_grain,
                f.feature_value,
                o.outcome_key,
                o.family,
                f.heat_id
        ) s
        GROUP BY
            feature_key,
            feature_grain,
            outcome_key,
            family
`;

text =
  text.slice(0, groupedStart) +
  groupedReplacement +
  text.slice(groupedEnd);

text = text.replaceAll(
  `EXCEPTION
        WHEN undefined_table OR undefined_column THEN
            NULL;`,
  `EXCEPTION
        WHEN OTHERS THEN
            NULL;`
);

fs.writeFileSync(file, text, "utf8");

console.log("Patched SQL 204: underflow-safe p-values + fixed categorical aggregation.");
