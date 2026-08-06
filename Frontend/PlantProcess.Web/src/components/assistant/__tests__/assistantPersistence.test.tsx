import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Outlet, Route, Routes, Link } from "react-router-dom";
import { describe, expect, it, vi, beforeEach } from "vitest";

/* PPIQ-T071 - THE POINT OF THE TASK.
 *
 * This is a routing-lifetime test, not a harness that keeps a component mounted
 * by hand. The topology mirrors the application: a PARENT route element holds
 * the provider and renders an Outlet, and routes A and B are its children. When
 * the child route changes, the parent element - and therefore the provider -
 * keeps its instance, exactly as AppLayout does around the real outlet.
 *
 * The conversation is created through the PUBLIC contract, by typing into the
 * real AssistantChat and letting the provider's ask() run, with assistantApi
 * mocked at the module boundary. No test-only setter was added to production
 * code to make this possible.
 */

vi.mock("@/api/assistantApi", () => ({
  assistantModeLabel: () => "grounded",
  assistantApi: {
    getAssistantConfig: vi.fn().mockResolvedValue({ allowedTools: [] }),
    /* AssistantChat renders answer.text - AssistantChat.tsx line 146. The first
       draft of this mock returned an "answer" field, which does not exist, so
       the bubble painted an empty <p> and the assertion failed correctly. */
    askAssistant: vi.fn().mockResolvedValue({
      text: "Grounded answer for the test.",
      citations: [],
      isRefusal: false,
    }),
  },
}));

import { AssistantDockProvider, useAssistantDock } from "../AssistantDockContext";
import { AssistantChat } from "../AssistantChat";

function Shell() {
  return (
    <AssistantDockProvider>
      <nav>
        <Link to="/a">go a</Link>
        <Link to="/b">go b</Link>
      </nav>
      <Outlet />
    </AssistantDockProvider>
  );
}

function Surface({ name }: { name: string }) {
  const { turns, busy, ask } = useAssistantDock();
  return (
    <div>
      <h1>{name}</h1>
      <p data-testid="turn-count">{turns.length}</p>
      <AssistantChat
        turns={turns}
        chips={["grounded"]}
        isBusy={busy}
        onAsk={(question) => void ask(question)}
        onOpenEvidence={() => undefined}
      />
    </div>
  );
}

function renderApp() {
  return render(
    <MemoryRouter initialEntries={["/a"]}>
      <Routes>
        <Route element={<Shell />}>
          <Route path="/a" element={<Surface name="route a" />} />
          <Route path="/b" element={<Surface name="route b" />} />
        </Route>
      </Routes>
    </MemoryRouter>,
  );
}

describe("T-071 the conversation survives navigation", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("a turn created on route A is still there on route B", async () => {
    const user = userEvent.setup();
    renderApp();
    expect(screen.getByText("route a")).toBeTruthy();

    const input = screen.getByRole("textbox");
    await user.type(input, "what caused the last quality hold?");
    await user.keyboard("{Enter}");

    await waitFor(() => {
      expect(Number(screen.getByTestId("turn-count").textContent)).toBeGreaterThanOrEqual(2);
    });
    const before = Number(screen.getByTestId("turn-count").textContent);

    await user.click(screen.getByText("go b"));
    expect(screen.getByText("route b")).toBeTruthy();

    /* The child route changed. If the provider had been remounted this would be
     * zero - the surviving count IS the behavioural proof. */
    expect(Number(screen.getByTestId("turn-count").textContent)).toBe(before);
    expect(screen.getByText(/Grounded answer for the test/)).toBeTruthy();
  });

  it("the assistant api is called through the provider exactly once per question", async () => {
    const user = userEvent.setup();
    const { assistantApi } = await import("@/api/assistantApi");
    renderApp();

    const input = screen.getByRole("textbox");
    await user.type(input, "one question");
    await user.keyboard("{Enter}");

    await waitFor(() => {
      expect(assistantApi.askAssistant).toHaveBeenCalledTimes(1);
    });

    await user.click(screen.getByText("go b"));
    expect(assistantApi.askAssistant).toHaveBeenCalledTimes(1);
  });
});