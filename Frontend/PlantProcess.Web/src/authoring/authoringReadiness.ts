// PPIQ T-242 Stage 3b. TWO QUESTIONS, TWO ANSWERS.
//
// The authoring surface used to ask one question - "is the board valid?" - and
// answer it from boardProblems alone. That was sufficient while every block a
// user could place was also a block the transformation representation could
// carry. It is no longer sufficient, and the gap was visible in the product:
//
//   a board carrying a compute block reported "Valid flow", because it IS
//   semantically valid; Run stayed enabled, because Run watched only the
//   validity reason; and pressing it logged "check the blocks that are marked
//   with an error" while no block was marked with anything.
//
// The board was fine. The SAVE BOUNDARY was not. Those are different facts and
// merging them produces a sentence that is untrue in both directions.
//
// This module holds that decision as pure data so it can be proven, rather
// than as a chain of ternaries inside JSX where it cannot.

export type AuthoringReadinessState = "ready" | "invalid" | "unsavable";

export interface AuthoringReadiness {
  readonly state: AuthoringReadinessState;
  /** The chip's short word. */
  readonly label: string;
  /** The full sentence, always present, never a shrug. */
  readonly detail: string;
  /** Whether an action that requires a saved definition may be offered. */
  readonly canRun: boolean;
}

const READY_DETAIL = "Every block has what it needs to run.";

/**
 * ORDER MATTERS AND IS DELIBERATE.
 *
 * A semantic problem is reported first, because it is the one the author can
 * fix by editing the board and it names a specific block. A save-boundary
 * refusal is reported only when the board itself is sound, because telling
 * someone their graph cannot be saved while a block is also missing its input
 * buries the actionable message under the structural one.
 */
export function authoringReadiness(
  invalidReason: string | null,
  serialisationRefusal: string | null,
): AuthoringReadiness {
  if (invalidReason) {
    return { state: "invalid", label: "Invalid", detail: invalidReason, canRun: false };
  }
  if (serialisationRefusal) {
    // NOT "Invalid". The board is valid; what is unavailable is saving it.
    // Calling this invalid would send the author hunting for a broken block
    // that does not exist.
    return {
      state: "unsavable",
      label: "Cannot be saved",
      detail: serialisationRefusal,
      canRun: false,
    };
  }
  return { state: "ready", label: "Valid flow", detail: READY_DETAIL, canRun: true };
}

/**
 * The sentence logged when an action is attempted anyway. It carries the
 * REASON rather than a generic instruction to go and look for a marked block,
 * because in the unsavable case there is no marked block to find.
 */
export function readinessBlockedMessage(readiness: AuthoringReadiness): string {
  if (readiness.state === "ready") {
    return READY_DETAIL;
  }
  if (readiness.state === "invalid") {
    return readiness.detail + " Fix the block it names, then run again.";
  }
  return readiness.detail;
}