// T-050. THE DRAWER'S EVIDENCE LOOKUP, AS A FUNCTION THAT CAN BE TESTED.
//
// Three outcomes must stay distinguishable, because they mean three different
// things to the person looking:
//
//   unavailable  the execution could not produce evidence and said so. The
//                server returns the values and adds an execution_evidence_
//                unavailable warning rather than writing a record it cannot
//                identify. Nothing is wrong; the evidence simply is not there.
//   notFound     the handle resolved to nothing - 404 from the resolver, which
//                the client already returns as null. The evidence is not
//                available to this tenant, or no longer exists.
//   error        the request itself failed. A network or server fault. This is
//                NOT the same as "no evidence" and must never be shown as it.
//
// Collapsing any two of these would be the honesty failure this feature exists
// to prevent: a drawer that says "no evidence" when the truth is "the request
// broke" teaches people to distrust the evidence that IS there.

export const EXECUTION_EVIDENCE_UNAVAILABLE = "execution_evidence_unavailable";

export type EvidenceLookup<TEvidence> =
  | { status: "resolved"; evidence: TEvidence }
  | { status: "unavailable"; reason: string }
  | { status: "notFound" }
  | { status: "error"; message: string };

/** The server's own sentence, kept verbatim. It names which prerequisite was
 *  missing, and paraphrasing it would lose that. */
export function executionEvidenceWarning(warnings: readonly string[] | null | undefined): string | null {
  if (!warnings) return null;
  return warnings.find((warning) => warning.startsWith(EXECUTION_EVIDENCE_UNAVAILABLE)) ?? null;
}

/**
 * Resolves an execution evidence handle through the EXISTING T-073 resolver.
 * The resolver is passed in rather than imported so this stays testable and so
 * no second evidence client can grow here.
 */
export async function resolveExecutionEvidence<TEvidence>(
  handle: { kind: string; id: string } | null | undefined,
  warnings: readonly string[] | null | undefined,
  resolve: (evidenceId: string) => Promise<TEvidence | null>,
): Promise<EvidenceLookup<TEvidence>> {
  const warning = executionEvidenceWarning(warnings);
  if (warning !== null) return { status: "unavailable", reason: warning };

  if (!handle || !handle.id) {
    return {
      status: "unavailable",
      reason: "This execution did not offer an evidence handle.",
    };
  }

  let evidence: TEvidence | null;
  try {
    evidence = await resolve(handle.id);
  } catch (error) {
    // A thrown resolver is a transport fault, never an absence of evidence.
    return { status: "error", message: error instanceof Error ? error.message : String(error) };
  }

  if (evidence === null || evidence === undefined) return { status: "notFound" };

  return { status: "resolved", evidence };
}

/** What the drawer prints for a population count. Null is a real answer -
 *  "not known" - and must never be replaced by the row count, the series
 *  length or the number of visible points. */
export function describePopulationCount(count: number | null | undefined): string {
  if (count === null || count === undefined) return "not reported by this source";
  return String(count);
}
