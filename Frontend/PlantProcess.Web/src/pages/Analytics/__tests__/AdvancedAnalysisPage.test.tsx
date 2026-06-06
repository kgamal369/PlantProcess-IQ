import { render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { AdvancedAnalysisPage } from "../AdvancedAnalysisPage";

vi.mock("../../../api/advancedAnalysis", () => ({
  getAnalysisReadiness: vi.fn(),
  getAdvancedResults: vi.fn(),
}));
import { getAnalysisReadiness, getAdvancedResults } from "../../../api/advancedAnalysis";

const rd = (overall: string, canRun: boolean) => ({
  overall, canRun, dimensions: [{ name: "independent_heats", state: overall, reason: "n below threshold" }],
  outcomeKey: "defect.edge_crack_rate", grain: "coil", windowDays: 30, independentHeats: 10, outcomeEvents: 10,
});

describe("AdvancedAnalysisPage (§7.4)", () => {
  beforeEach(() => vi.clearAllMocks());

  it("shows the blocked state with per-dimension reasons", async () => {
    (getAnalysisReadiness as unknown as { mockResolvedValue: (v: unknown) => void }).mockResolvedValue(rd("Blocked", false));
    (getAdvancedResults as unknown as { mockResolvedValue: (v: unknown) => void }).mockResolvedValue([]);
    render(<MemoryRouter initialEntries={["/investigate/advanced?outcomeKey=defect.edge_crack_rate"]}><AdvancedAnalysisPage /></MemoryRouter>);
    await waitFor(() => expect(screen.getByText(/blocked by the data-readiness gate/i)).toBeInTheDocument());
  });

  it("renders ranked contributors with method + honesty caveat", async () => {
    (getAnalysisReadiness as unknown as { mockResolvedValue: (v: unknown) => void }).mockResolvedValue(rd("Ready", true));
    (getAdvancedResults as unknown as { mockResolvedValue: (v: unknown) => void }).mockResolvedValue([{
      findingId: "casting_speed", method: "Spearman", effectSize: 0.6, qValue: 0.01, sampleSize: 90,
      stabilityLower: 0.4, stabilityUpper: 0.8, stabilityConsistency: 0.98, isStable: true,
      survivesStratification: true, significant: true, isRenderable: true,
      honestyCaveat: "This is a diagnostic association, not a evidence-backed root-cause investigation.",
    }]);
    render(<MemoryRouter initialEntries={["/investigate/advanced?outcomeKey=defect.edge_crack_rate"]}><AdvancedAnalysisPage /></MemoryRouter>);
    await waitFor(() => expect(screen.getByText(/Spearman/)).toBeInTheDocument());
    expect(screen.getByText(/diagnostic association/i)).toBeInTheDocument();
  });
});
