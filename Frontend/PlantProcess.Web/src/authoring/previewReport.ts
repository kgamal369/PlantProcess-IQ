// PPIQ T-035. WHAT THE DEBUG LOG SAYS ABOUT A PREVIEW.
//
// Chapter 4 section 5.2.8: entries typed Error, Warning or Success, each with a
// message written for a plant engineer, plus the returned row count and a cost
// estimate. Never a raw exception string.
//
// The decision lives here rather than inside the click handler so the three
// cases the task names are asserted as facts about a function: a valid preview,
// a preview returning nothing, and a rejected operator.
//
// THE ESTIMATE IS AN ESTIMATE. plannerCost comes from EXPLAIN, which plans the
// statement and runs nothing. It is not a runtime and it is not a price, and
// every sentence here calls it what it is.

import type { DryRunResult } from "@/api/canvasApi";

export type PreviewSeverity = "success" | "warning" | "error";

export interface PreviewReport {
  severity: PreviewSeverity;
  message: string;
  facts?: string;
}

/** The planner's numbers, or an empty string when the server sent neither. */
export function describeEstimate(
  plannerCost: number | null | undefined, estimatedRows: number | null | undefined,
): string {
  const parts: string[] = [];
  if (typeof plannerCost === "number") {
    parts.push("planner cost estimate " + Math.round(plannerCost).toLocaleString("en-US"));
  }
  if (typeof estimatedRows === "number") {
    parts.push("planner estimates about " + estimatedRows.toLocaleString("en-US") + " rows");
  }
  return parts.join(", ");
}

/** The measured facts of a preview: what came back, and how long it took. */
export function describeMeasured(result: DryRunResult, elapsedMs: number): string {
  const parts: string[] = [];
  // "sample rows" and not "rows": the preview stops at a limit, and when it did
  // stop the entry says so rather than presenting the cap as a total.
  parts.push(result.previewTruncated === true
    ? result.rowCount + " sample rows, stopped at the preview limit"
    : result.rowCount + " sample rows");
  parts.push(result.columns.length + " columns: " + result.columns.join(", "));
  parts.push("elapsed " + elapsedMs + " ms");
  const estimate = describeEstimate(result.plannerCost, result.estimatedRows);
  if (estimate !== "") { parts.push(estimate); }
  return parts.join(" | ");
}

export function describePreview(result: DryRunResult, elapsedMs: number): PreviewReport {
  if (result.status === "succeeded") {
    if (result.rowCount === 0) {
      // A query that ran perfectly and matched nothing is not a success to an
      // engineer who expected rows. It is also NOT evidence that the filter is
      // wrong - the server has no way to know which of the two it is, so the
      // sentence names both possibilities and claims neither.
      return {
        severity: "warning",
        message: "Preview completed successfully but returned 0 rows."
          + " Review the active filters or confirm that the selected source contains matching rows.",
      };
    }
    return { severity: "success", message: "Preview ran.", facts: describeMeasured(result, elapsedMs) };
  }

  // Everything else is a refusal. The server's sentence already names what was
  // wrong - the operator, the column, the table - and it is passed through
  // WHOLE, because rewording it here would lose the one specific the engineer
  // needs. The server guarantees it is safe to show.
  const sentence = (result.message ?? "").trim();
  if (sentence === "") {
    return {
      severity: "error",
      message: "The preview was refused and no reason came back with it."
        + " Run it again, and if the refusal repeats without a reason, report this definition.",
    };
  }
  return { severity: "error", message: "The preview was refused. " + sentence };
}

/**
 * What the log says when the request itself threw.
 *
 * IT READS NOTHING FROM THE THROWN VALUE. A fetch failure, a JSON parse error
 * or a whole stack trace are all things a plant engineer cannot act on and
 * should never be shown. The sentence says what to check instead.
 */
export function describeThrownPreview(_thrown: unknown): string {
  return "The preview did not complete, because the request to the server did not return."
    + " Check that the API is running, then run it again.";
}

/**
 * The same guarantee for every OTHER action in the shell - publish, fork, run
 * SQL, save SQL. Each of those handlers passed the thrown value straight into
 * the log, so the prohibition was only half kept while four routes to a raw
 * exception string were still open.
 */
export function describeThrownAction(_thrown: unknown): string {
  return "That action did not complete, because the request to the server did not return."
    + " Check that the API is running, then try it again.";
}
