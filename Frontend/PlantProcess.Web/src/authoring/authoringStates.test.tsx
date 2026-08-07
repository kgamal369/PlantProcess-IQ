// PPIQ T-040 acceptance for the seven states, G12 to G18.
//
// The three distinctions the gate requires are each a test, and each fails for
// the right reason: not because a class name changed, but because two different
// facts would be reported to a plant engineer as the same thing.

import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { AuthoringStateBanner } from "./AuthoringStateBanner";
import {
  ALL_AUTHORING_STATES, describeAuthoringState, resolveAuthoringState,
  toAuthoringStateFacts,
  type AuthoringState, type AuthoringStateFacts, type ShellRunInput,
} from "./authoringStates";

const QUIET: AuthoringStateFacts = {
  running: false, failure: null, refusal: null, blocker: null, rowCount: null, filtered: false,
};
const facts = (over: Partial<AuthoringStateFacts>): AuthoringStateFacts => ({ ...QUIET, ...over });

const RUN_OF = (over: Partial<AuthoringStateFacts>) =>
  describeAuthoringState(facts(over));

describe("T-040 the seven states resolve from real facts", () => {
  it("declares exactly the seven the gate names, in G12 to G18 order", () => {
    expect(ALL_AUTHORING_STATES).toEqual([
      "empty", "loading", "populated", "filtered-empty", "blocked", "refused", "failed",
    ]);
  });

  it("reaches every one of the seven from some combination of facts", () => {
    const reached = new Set<AuthoringState>([
      resolveAuthoringState(facts({})),
      resolveAuthoringState(facts({ running: true })),
      resolveAuthoringState(facts({ rowCount: 12 })),
      resolveAuthoringState(facts({ rowCount: 0, filtered: true })),
      resolveAuthoringState(facts({ blocker: "Add a dataset first." })),
      resolveAuthoringState(facts({ refusal: "That column is not published." })),
      resolveAuthoringState(facts({ failure: "The request did not return." })),
    ]);
    expect(Array.from(reached).sort()).toEqual([...ALL_AUTHORING_STATES].sort());
  });

  it("puts a failure above everything, because nothing below it is known", () => {
    expect(resolveAuthoringState(facts({
      failure: "no answer", refusal: "r", blocker: "b", running: true, rowCount: 5,
    }))).toBe("failed");
  });

  it("puts a refusal above a blocker, because the server already answered", () => {
    expect(resolveAuthoringState(facts({ refusal: "r", blocker: "b" }))).toBe("refused");
  });

  it("shows loading rather than a result that is about to be replaced", () => {
    expect(resolveAuthoringState(facts({ running: true, rowCount: 9 }))).toBe("loading");
  });
});

describe("T-040 the three required distinctions", () => {
  it("EMPTY is not FILTERED-EMPTY, in state and in wording", () => {
    const empty = RUN_OF({ rowCount: 0, filtered: false });
    const filtered = RUN_OF({ rowCount: 0, filtered: true });

    expect(empty.state).toBe("empty");
    expect(filtered.state).toBe("filtered-empty");
    expect(empty.sentence).not.toBe(filtered.sentence);
    // Both sentences mention filters, and that is the point: one says the
    // filters removed everything, the other says there are none to blame. An
    // earlier version of this test asserted the WORD was absent from empty,
    // which tested the vocabulary rather than the distinction.
    expect(filtered.sentence).toContain("narrowed the result to nothing");
    expect(empty.sentence).toContain("carries no filters");
    expect(empty.sentence).toContain("source itself holds nothing");
    // The one that matters: filtered-empty must not read as an absence of data.
    expect(filtered.sentence).toContain("scope answer");
  });

  it("BLOCKED is not REFUSED, and each says which it is", () => {
    const blocked = RUN_OF({ blocker: "Add a dataset node before running." });
    const refused = RUN_OF({ refusal: "That column is not published to this purpose." });

    expect(blocked.state).toBe("blocked");
    expect(refused.state).toBe("refused");
    expect(blocked.sentence).toContain("dataset node");
    expect(refused.sentence).toContain("not published");
    expect(blocked.heading).not.toBe(refused.heading);
  });

  it("REFUSED is not FAILED: one is an answer, the other is the absence of one", () => {
    const refused = RUN_OF({ refusal: "The expression names an unknown keyword." });
    const failed = RUN_OF({ failure: "The request to the server did not return." });

    expect(refused.state).toBe("refused");
    expect(failed.state).toBe("failed");
    expect(refused.tone).toBe("amber");
    expect(failed.tone).toBe("danger");
    expect(refused.nextAction).not.toBe(failed.nextAction);
  });
});

describe("T-040 colour never carries the meaning", () => {
  it("gives every non-populated state a sentence, not just a tone", () => {
    const cases: Partial<AuthoringStateFacts>[] = [
      {}, { running: true }, { rowCount: 0, filtered: true },
      { blocker: "b" }, { refusal: "r" }, { failure: "f" },
    ];
    for (const over of cases) {
      const described = RUN_OF(over);
      expect(described.sentence.length, described.state).toBeGreaterThan(0);
    }
  });

  it("tells the author what to do wherever there is something to do", () => {
    for (const over of [{}, { rowCount: 0, filtered: true }, { blocker: "b" }, { refusal: "r" }, { failure: "f" }]) {
      const described = RUN_OF(over);
      expect(described.nextAction, described.state).toBeTruthy();
    }
    // Running and populated are the two with nothing to ask of the author.
    expect(RUN_OF({ running: true }).nextAction).toBeNull();
    expect(RUN_OF({ rowCount: 3 }).nextAction).toBeNull();
  });

  it("renders the state as data, the wording as text, and never a bare outline", () => {
    render(<AuthoringStateBanner facts={facts({ refusal: "That column is not published." })} />);
    const banner = screen.getByTestId("authoring-state");
    expect(banner).toHaveAttribute("data-state", "refused");
    expect(banner).toHaveTextContent("That column is not published.");
    expect(screen.getByTestId("authoring-state-action")).toBeInTheDocument();
  });

  it("announces a failure to a screen reader rather than only colouring it", () => {
    render(<AuthoringStateBanner facts={facts({ failure: "The request did not return." })} />);
    expect(screen.getByTestId("authoring-state")).toHaveAttribute("role", "alert");
  });

  it("renders every one of the seven without a missing heading", () => {
    const cases: Partial<AuthoringStateFacts>[] = [
      {}, { running: true }, { rowCount: 4 }, { rowCount: 0, filtered: true },
      { blocker: "b" }, { refusal: "r" }, { failure: "f" },
    ];
    for (const over of cases) {
      const view = render(<AuthoringStateBanner facts={facts(over)} />);
      const banner = view.getByTestId("authoring-state");
      expect(banner.textContent?.trim().length, String(banner.getAttribute("data-state"))).toBeGreaterThan(0);
      view.unmount();
    }
  });
});

describe("T-040 03a2 the shell's own values become the seven states", () => {
  const shell = (over: Partial<ShellRunInput>): ShellRunInput => ({
    running: false, failure: null, refusal: null, blocker: null,
    rowCount: null, filtered: false, ...over,
  });
  const stateOf = (over: Partial<ShellRunInput>) =>
    resolveAuthoringState(toAuthoringStateFacts(shell(over)));

  it("a started run is Loading", () => {
    expect(stateOf({ running: true })).toBe("loading");
  });

  it("a stale previous result does not mask Loading", () => {
    // The row count from the last run is still in state while the next one
    // flies. Reporting Populated here would show the old answer as the new one.
    expect(stateOf({ running: true, rowCount: 42 })).toBe("loading");
    expect(toAuthoringStateFacts(shell({ running: true, rowCount: 42 })).rowCount).toBeNull();
  });

  it("rows returned are Populated", () => {
    expect(stateOf({ rowCount: 42 })).toBe("populated");
  });

  it("zero rows with no filters is Empty", () => {
    expect(stateOf({ rowCount: 0, filtered: false })).toBe("empty");
  });

  it("zero rows with filters is Filtered-empty", () => {
    expect(stateOf({ rowCount: 0, filtered: true })).toBe("filtered-empty");
  });

  it("an unmet prerequisite is Blocked", () => {
    expect(stateOf({ blocker: "Nothing on the board yet." })).toBe("blocked");
  });

  it("a server refusal is Refused, never Failed", () => {
    expect(stateOf({ refusal: "Refused (42703). column does not exist" })).toBe("refused");
  });

  it("a transport failure is Failed, never Refused", () => {
    expect(stateOf({ failure: "That action did not complete." })).toBe("failed");
  });

  it("a successful retry clears the previous Failed", () => {
    // The shell clears lastRunFailure at RUN START, so the next success cannot
    // carry the old failure with it.
    expect(stateOf({ failure: "That action did not complete." })).toBe("failed");
    expect(stateOf({ failure: null, rowCount: 7 })).toBe("populated");
  });

  it("keeps Refused separate from Failed even when both are somehow present", () => {
    // Failure wins on precedence, but the two are never conflated into one
    // wording: the descriptor still carries the transport sentence.
    const described = describeAuthoringState(
      toAuthoringStateFacts(shell({ failure: "did not complete", refusal: "refused sentence" })));
    expect(described.state).toBe("failed");
    expect(described.sentence).toBe("did not complete");
  });
});