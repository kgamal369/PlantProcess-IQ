// T-051. THE SEVEN CANONICAL STATES, REACHED FROM REAL WIDGET FACTS.
//
// These assertions are about the presenter and the T-040 authority it consumes.
// They deliberately do not re-test resolveAuthoringState's own precedence table,
// which authoringStates.test.tsx already owns - they prove the WIDGET reaches
// each state and tells each one apart in words.

import { render } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { WidgetStatePanel, WIDGET_RENDER_FAILURE_FACTS } from "./WidgetStatePanel";
import type { AuthoringStateFacts } from "@/authoring/authoringStates";

const BASE: AuthoringStateFacts = {
  running: false, failure: null, refusal: null,
  blocker: null, rowCount: 12, filtered: false,
};

function panel(overrides: Partial<AuthoringStateFacts>) {
  // Scoped to THIS render's own container. render() leaves earlier containers
  // in document.body, so a document-wide query returns every panel the test has
  // mounted so far and fails on the second assertion in any test.
  const view = render(<WidgetStatePanel facts={{ ...BASE, ...overrides }} />);
  const node = view.container.querySelector("[data-testid='widget-state']");
  return { view, node };
}

function stateOf(overrides: Partial<AuthoringStateFacts>) {
  const { node } = panel(overrides);
  return node?.getAttribute("data-widget-state") ?? null;
}

function toneOf(overrides: Partial<AuthoringStateFacts>) {
  const { node } = panel(overrides);
  return node?.getAttribute("data-widget-tone") ?? null;
}

function textOf(overrides: Partial<AuthoringStateFacts>) {
  const { node } = panel(overrides);
  return (node?.textContent ?? "").trim();
}

describe("T-051 widget state panel", () => {
  it("reaches all seven canonical states from widget facts", () => {
    expect(stateOf({ rowCount: 12 })).toBe("populated");
    expect(stateOf({ running: true })).toBe("loading");
    expect(stateOf({ rowCount: null })).toBe("empty");
    expect(stateOf({ rowCount: 0, filtered: false })).toBe("empty");
    expect(stateOf({ rowCount: 0, filtered: true })).toBe("filtered-empty");
    expect(stateOf({ blocker: "The value column is not returned." })).toBe("blocked");
    expect(stateOf({ refusal: "This renderer is unavailable." })).toBe("refused");
    expect(stateOf({ failure: "The query did not complete." })).toBe("failed");
  });

  it("keeps the canonical precedence rather than restating it", () => {
    // failure outranks everything, refusal outranks loading, loading outranks
    // a blocker. If T-040 ever changes, this fails here rather than drifting.
    expect(stateOf({ failure: "f", refusal: "r", running: true, blocker: "b", rowCount: 0 }))
      .toBe("failed");
    expect(stateOf({ refusal: "r", running: true, blocker: "b" })).toBe("refused");
    expect(stateOf({ running: true, blocker: "b" })).toBe("loading");
  });

  it("carries the canonical tone for every state", () => {
    expect(toneOf({ blocker: "b" })).toBe("amber");
    expect(toneOf({ refusal: "r" })).toBe("amber");
    expect(toneOf({ failure: "f" })).toBe("danger");
    expect(toneOf({ rowCount: null })).toBe("muted");
    expect(toneOf({ running: true })).toBe("muted");
    expect(toneOf({ rowCount: 0, filtered: true })).toBe("muted");
  });

  it("tells empty apart from filtered-empty in words, not colour", () => {
    const empty = textOf({ rowCount: 0, filtered: false });
    const filtered = textOf({ rowCount: 0, filtered: true });

    expect(empty).not.toBe(filtered);
    expect(empty.toLowerCase()).toContain("nothing here");
    expect(filtered.toLowerCase()).toContain("selection");
    // Both are muted, so wording is the only carrier of the distinction.
    expect(toneOf({ rowCount: 0, filtered: false }))
      .toBe(toneOf({ rowCount: 0, filtered: true }));
  });

  it("tells blocked apart from refused, and refused apart from failed", () => {
    const blocked = textOf({ blocker: "The bound value column is gone." });
    const refused = textOf({ refusal: "That renderer is unavailable." });
    const failed = textOf({ failure: "The query did not complete." });

    expect(blocked).not.toBe(refused);
    expect(refused).not.toBe(failed);
    expect(blocked).not.toBe(failed);
    expect(blocked).toContain("The bound value column is gone.");
    expect(refused).toContain("That renderer is unavailable.");
  });

  it("shows the render-failure fallback without any exception text", () => {
    const text = textOf(WIDGET_RENDER_FAILURE_FACTS);
    expect(stateOf(WIDGET_RENDER_FAILURE_FACTS)).toBe("failed");
    expect(text).toContain("could not be displayed");
    // No exception class, no file/line, no stack frame. Not "at " - the
    // descriptor's own next action says "Check that the API is running", and
    // "that the" contains it.
    expect(text).not.toContain("Error");
    expect(text).not.toContain(".ts:");
    expect(text).not.toContain("    at");
  });

  it("announces a failure assertively and other states politely", () => {
    expect(panel({ failure: "f" }).node?.getAttribute("role")).toBe("alert");
    expect(panel({ rowCount: 0 }).node?.getAttribute("role")).toBe("status");
  });
});
