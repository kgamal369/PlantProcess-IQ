// PPIQ T-040. THE SEVEN STATES, DECIDED IN ONE PLACE.
//
// Chapter 2 line 17 and Golden Gate lines G12 to G18: every surface implements
// Empty, Loading, Populated, Filtered-empty, Blocked, Refused and Failed, each
// with its own wording. Three distinctions are required and are the reason this
// module exists rather than a handful of ternaries in a component:
//
//   EMPTY is not FILTERED-EMPTY. Nothing here yet is a different fact from
//   nothing matched what you asked for, and wording them alike tells an
//   engineer his data is missing when his filter is simply narrow.
//
//   BLOCKED is not REFUSED. Blocked means a precondition is unmet and names it,
//   so the author knows what to do next. Refused means the request was
//   understood and rejected, and carries the sentence that says why.
//
//   REFUSED is not FAILED. Refused is an answer. Failed is the absence of one -
//   the request did not complete - and the two demand different next actions.
//
// COLOUR IS NEVER THE CARRIER. G17 is explicit that a red outline with no
// sentence beside it is a specification failure, so every non-populated state
// here carries wording, and where the author can act, the action.

export type AuthoringState =
  | "empty"
  | "loading"
  | "populated"
  | "filtered-empty"
  | "blocked"
  | "refused"
  | "failed";

/** The gate's palette roles. The component maps these to classes, once. */
export type AuthoringStateTone = "muted" | "amber" | "danger" | "none";

/**
 * Everything the shell already knows, named as facts rather than as states.
 * The shell fills this in from what it actually holds; nothing here is a flag
 * invented so a test can reach a branch.
 */
export interface AuthoringStateFacts {
  /** A run is in flight right now. */
  running: boolean;
  /** The transport did not complete. Failed outranks every other state. */
  failure: string | null;
  /** The server understood the request and rejected it, with its sentence. */
  refusal: string | null;
  /** A precondition is unmet, named so the author can satisfy it. */
  blocker: string | null;
  /** Null means no run has produced a result yet. */
  rowCount: number | null;
  /** Whether the definition currently narrows what it returns. */
  filtered: boolean;
}

/**
 * PRECEDENCE, and it is deliberate. A failure hides everything because nothing
 * below it is known. A refusal outranks a blocker because the server has
 * already answered. Loading outranks a stale result because the result is about
 * to be replaced.
 */
export function resolveAuthoringState(facts: AuthoringStateFacts): AuthoringState {
  if (facts.failure) { return "failed"; }
  if (facts.refusal) { return "refused"; }
  if (facts.running) { return "loading"; }
  if (facts.blocker) { return "blocked"; }
  if (facts.rowCount === null) { return "empty"; }
  if (facts.rowCount === 0) { return facts.filtered ? "filtered-empty" : "empty"; }
  return "populated";
}

export interface AuthoringStateDescriptor {
  state: AuthoringState;
  tone: AuthoringStateTone;
  /** Two or three words. Never the only thing on screen. */
  heading: string;
  /** What is true right now, in a sentence. */
  sentence: string;
  /** What the author can do about it, where there is something to do. */
  nextAction: string | null;
}

const TONES: Record<AuthoringState, AuthoringStateTone> = {
  "empty": "muted",
  "loading": "muted",
  "populated": "none",
  "filtered-empty": "muted",
  "blocked": "amber",
  "refused": "amber",
  "failed": "danger",
};

/**
 * The words. They take the shell's own sentences where the shell has one - a
 * refusal already carries the server's reason and a blocker already names the
 * missing precondition - and supply the rest. Nothing here invents a cause.
 */
export function describeAuthoringState(
  facts: AuthoringStateFacts,
): AuthoringStateDescriptor {
  const state = resolveAuthoringState(facts);
  const tone = TONES[state];

  if (state === "failed") {
    return {
      state, tone,
      heading: "Did not complete",
      sentence: facts.failure ?? "",
      nextAction: "Check that the API is running, then try it again.",
    };
  }

  if (state === "refused") {
    return {
      state, tone,
      heading: "Refused",
      sentence: facts.refusal ?? "",
      nextAction: "Change the definition to satisfy the rule above, then run it again.",
    };
  }

  if (state === "loading") {
    return {
      state, tone,
      heading: "Running",
      sentence: "The definition is running against the server.",
      nextAction: null,
    };
  }

  if (state === "blocked") {
    return {
      state, tone,
      heading: "Not ready to run",
      sentence: facts.blocker ?? "",
      nextAction: "Complete the step named above, then run it.",
    };
  }

  if (state === "filtered-empty") {
    return {
      state, tone,
      heading: "No rows matched",
      // The distinction the gate cares about: this says the filter narrowed
      // everything away, NOT that there is nothing to show.
      sentence: "The definition ran and returned 0 rows. The filters it carries"
        + " narrowed the result to nothing, so this is a scope answer rather than"
        + " an absence of data.",
      nextAction: "Widen or remove a filter, then run it again.",
    };
  }

  if (state === "empty") {
    return {
      state, tone,
      heading: facts.rowCount === null ? "Nothing run yet" : "No rows",
      sentence: facts.rowCount === null
        ? "This definition has not been run, so there is nothing to show yet."
        : "The definition ran and returned 0 rows, and it carries no filters -"
          + " so the source itself holds nothing matching it.",
      nextAction: facts.rowCount === null
        ? "Run the definition to see what it returns."
        : "Confirm the source contains rows this definition can reach.",
    };
  }

  return {
    state, tone,
    heading: "Result",
    sentence: String(facts.rowCount) + " rows returned.",
    nextAction: null,
  };
}

/** Every state, in gate order G12 to G18. Used by the certification test. */
export const ALL_AUTHORING_STATES: readonly AuthoringState[] = [
  "empty", "loading", "populated", "filtered-empty", "blocked", "refused", "failed",
];