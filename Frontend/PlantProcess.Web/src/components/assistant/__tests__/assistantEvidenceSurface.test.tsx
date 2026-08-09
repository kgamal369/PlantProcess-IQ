import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { describe, expect, it, vi } from "vitest";

import { AssistantChat } from "../AssistantChat";
import type { AssistantAnswer, AssistantWidgetResultEvidence } from "@/api/assistantApi";
import { EVIDENCE_FOCUS_CLASS, applyEvidenceFocus, findWidgetElement } from "@/pages/Dashboard/evidenceFocus";

/* PPIQ-T075. The evidence surface.
 *
 * Nothing here names a real page, a real widget or an industry term. If the
 * tests needed one to pass, the implementation would be reading the wrong
 * things. */

const EVIDENCE_ID = "11111111-1111-1111-1111-111111111111";

function evidence(overrides: Partial<AssistantWidgetResultEvidence> = {}): AssistantWidgetResultEvidence {
  return {
    evidenceId: EVIDENCE_ID,
    available: true,
    pageCode: "PAGE_ALPHA",
    widgetCode: "WIDGET_ALPHA",
    chartType: "bar",
    dimensionCode: "DIM_ALPHA",
    measureCode: "MEASURE_ALPHA",
    filterContext: "{}",
    generatedAtUtc: "2026-08-08T20:00:00Z",
    columns: ["dimensionLabel", "value"],
    rows: [["LABEL_ONE", "12.5"]],
    hasObservationCount: true,
    observationCountTotal: 900,
    sentence: "On page PAGE_ALPHA, widget WIDGET_ALPHA reported LABEL_ONE 12.5.",
    ...overrides,
  };
}

function answer(kind = "WidgetResult"): AssistantAnswer {
  return {
    text: "A composed answer whose prose must never be used as the evidence detail.",
    citations: [{ kind, id: EVIDENCE_ID }],
    isRefusal: false,
    refusalReason: null,
    blocked: [],
  } as unknown as AssistantAnswer;
}

function renderChat(props: Partial<Parameters<typeof AssistantChat>[0]> = {}) {
  return render(
    <MemoryRouter>
      <AssistantChat turns={[{ role: "assistant", answer: answer() }]} {...props} />
    </MemoryRouter>,
  );
}

describe("T-075 citation chips", () => {
  it("A. renders a citation as an accessible chip", async () => {
    renderChat({ loadEvidence: vi.fn() });

    const chip = screen.getByTestId("assistant-citation");
    expect(chip.tagName).toBe("BUTTON");
    expect(chip).toHaveAttribute("aria-expanded", "false");
    expect(chip).toHaveAttribute("aria-controls");
    /* The full id stays reachable even though the visible label is short. */
    expect(chip).toHaveAttribute("title", expect.stringContaining(EVIDENCE_ID));
  });

  it("B. fetches evidence only when the chip is opened", async () => {
    const load = vi.fn().mockResolvedValue(evidence());
    renderChat({ loadEvidence: load });

    expect(load).not.toHaveBeenCalled();

    await userEvent.click(screen.getByTestId("assistant-citation"));

    await waitFor(() => expect(load).toHaveBeenCalledTimes(1));
    expect(load).toHaveBeenCalledWith(EVIDENCE_ID);
  });

  it("C. renders the resolved evidence, not the answer prose", async () => {
    renderChat({ loadEvidence: vi.fn().mockResolvedValue(evidence()) });

    await userEvent.click(screen.getByTestId("assistant-citation"));

    const sentence = await screen.findByTestId("assistant-evidence-sentence");
    expect(sentence).toHaveTextContent("reported LABEL_ONE 12.5");
    expect(sentence).not.toHaveTextContent("composed answer");

    const strip = screen.getByTestId("assistant-evidence");
    expect(strip).toHaveTextContent("WIDGET_ALPHA");
    expect(strip).toHaveTextContent("observationCount total");
    /* The word the ruling forbids. */
    expect(strip).not.toHaveTextContent(/population/i);
  });

  it("D. closing and reopening toggles without refetching", async () => {
    const load = vi.fn().mockResolvedValue(evidence());
    renderChat({ loadEvidence: load });

    const chip = screen.getByTestId("assistant-citation");

    await userEvent.click(chip);
    await screen.findByTestId("assistant-evidence");
    expect(chip).toHaveAttribute("aria-expanded", "true");

    await userEvent.click(chip);
    expect(screen.queryByTestId("assistant-evidence")).toBeNull();
    expect(chip).toHaveAttribute("aria-expanded", "false");

    await userEvent.click(chip);
    await screen.findByTestId("assistant-evidence");
    expect(load).toHaveBeenCalledTimes(1);
  });

  it("E. unavailable evidence says so truthfully", async () => {
    renderChat({ loadEvidence: vi.fn().mockResolvedValue(null) });

    await userEvent.click(screen.getByTestId("assistant-citation"));

    await screen.findByTestId("assistant-evidence-unavailable");
    expect(screen.queryByTestId("assistant-evidence-failed")).toBeNull();
  });

  it("F. a transport failure is a different state from unavailable", async () => {
    renderChat({ loadEvidence: vi.fn().mockRejectedValue(new Error("network down")) });

    await userEvent.click(screen.getByTestId("assistant-citation"));

    const failed = await screen.findByTestId("assistant-evidence-failed");
    expect(failed).toHaveTextContent("technical fault");
    expect(screen.queryByTestId("assistant-evidence-unavailable")).toBeNull();
  });

  it("G. Open in page uses the evidence page and widget identity", async () => {
    renderChat({ loadEvidence: vi.fn().mockResolvedValue(evidence()) });

    await userEvent.click(screen.getByTestId("assistant-citation"));

    const open = await screen.findByTestId("assistant-open-in-page");
    expect(open).toHaveAttribute("data-href", "/workspace/PAGE_ALPHA?focusWidget=WIDGET_ALPHA");
  });

  it("a kind with no detailed evidence says so instead of inventing rows", async () => {
    render(
      <MemoryRouter>
        <AssistantChat turns={[{ role: "assistant", answer: answer("Dataset") }]} loadEvidence={vi.fn()} />
      </MemoryRouter>,
    );

    await userEvent.click(screen.getByTestId("assistant-citation"));

    await screen.findByTestId("assistant-evidence-nodetail");
    expect(screen.queryByTestId("assistant-evidence-rows")).toBeNull();
  });
});

describe("T-075 suggested questions", () => {
  it("H. no global hardcoded list survives", () => {
    render(
      <MemoryRouter>
        <AssistantChat turns={[]} starters={[]} />
      </MemoryRouter>,
    );

    /* The retired global list asked the same three questions on every page,
       whatever was on it. Its absence is the assertion. */
    expect(screen.getByTestId("assistant-no-starters")).toBeInTheDocument();
    /* Proving the absence by naming the retired question would reintroduce it.
       No button at all in the starter region is the stronger claim. */
    expect(screen.getByTestId("assistant-starters").querySelectorAll("button")).toHaveLength(0);
  });

  it("I. two contexts produce different starters", () => {
    const first = render(
      <MemoryRouter>
        <AssistantChat turns={[]} starters={["What does WIDGET_ALPHA show?"]} />
      </MemoryRouter>,
    );
    expect(screen.getByText("What does WIDGET_ALPHA show?")).toBeInTheDocument();
    first.unmount();

    render(
      <MemoryRouter>
        <AssistantChat turns={[]} starters={["What does WIDGET_BETA show?"]} />
      </MemoryRouter>,
    );
    expect(screen.getByText("What does WIDGET_BETA show?")).toBeInTheDocument();
    expect(screen.queryByText("What does WIDGET_ALPHA show?")).toBeNull();
  });
});

describe("T-075 focusing the evidence-owning widget", () => {
  it("J. finds the real widget by its persisted code and marks it briefly", () => {
    document.body.innerHTML =
      '<div data-widget-code="WIDGET_OTHER"></div><div data-widget-code="WIDGET_ALPHA"></div>';

    const target = findWidgetElement(document, "WIDGET_ALPHA");
    expect(target).not.toBeNull();

    const timeouts: Array<() => void> = [];
    applyEvidenceFocus(target!, { setTimeout: ((fn: () => void) => { timeouts.push(fn); return 0; }) as never });

    expect(target!.classList.contains(EVIDENCE_FOCUS_CLASS)).toBe(true);

    timeouts.forEach((fn) => fn());
    expect(target!.classList.contains(EVIDENCE_FOCUS_CLASS)).toBe(false);
  });

  it("returns nothing when the historical widget no longer exists", () => {
    document.body.innerHTML = '<div data-widget-code="WIDGET_OTHER"></div>';

    expect(findWidgetElement(document, "WIDGET_GONE")).toBeNull();
    expect(findWidgetElement(document, "")).toBeNull();
  });
});