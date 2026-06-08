import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { AdvancedAnalysisPage } from "../AdvancedAnalysisPage";

vi.mock("../../../api/advancedAnalysis", () => ({
  getAnalysisReadiness: vi.fn(),
  getAnalysisReadinessGates: vi.fn(),
  getAdvancedResults: vi.fn(),
}));

import {
  getAdvancedResults,
  getAnalysisReadiness,
  getAnalysisReadinessGates,
} from "../../../api/advancedAnalysis";

const rd = (overall: string, canRun: boolean) => ({
  overall,
  canRun,
  dimensions: [
    {
      name: "independent_heats",
      code: "independent_heats",
      label: "Independent heats",
      displayName: "Independent heats",
      dimensionCode: "independent_heats",
      state: overall,
      status: overall,
      reason: "n below threshold",
      message: "n below threshold",
    },
  ],
  outcomeKey: "defect.edge_crack_rate",
  grain: "coil",
  windowDays: 30,
  independentHeats: 10,
  outcomeEvents: 10,
});

const gates = (overall: string) => ({
  overall,
  outcomeKey: "defect.edge_crack_rate",
  grain: "coil",
  windowDays: 30,
  readyCount: overall === "Ready" ? 2 : 0,
  partialCount: overall === "Partial" ? 2 : 0,
  blockedCount: overall === "Blocked" ? 2 : 0,
  blockingGateCount: overall === "Blocked" ? 2 : 0,
  gates: [
    {
      code: "population",
      gateCode: "population",
      name: "Population",
      label: "Population",
      displayName: "Population",
      state: overall,
      status: overall,
      reason: "n below threshold",
      message: "n below threshold",
      isBlocking: overall === "Blocked",
    },
    {
      code: "stratification",
      gateCode: "stratification",
      name: "Stratification",
      label: "Stratification",
      displayName: "Stratification",
      state: overall,
      status: overall,
      reason: "not enough independent heats",
      message: "not enough independent heats",
      isBlocking: overall === "Blocked",
    },
  ],
});

describe("AdvancedAnalysisPage (§7.4)", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(getAnalysisReadinessGates).mockResolvedValue(gates("Ready") as never);
  });

  it("shows the blocked state with per-dimension reasons", async () => {
    vi.mocked(getAnalysisReadiness).mockResolvedValue(rd("Blocked", false) as never);
    vi.mocked(getAnalysisReadinessGates).mockResolvedValue(gates("Blocked") as never);
    vi.mocked(getAdvancedResults).mockResolvedValue([] as never);

    render(
      <MemoryRouter initialEntries={["/investigate/advanced?outcomeKey=defect.edge_crack_rate"]}>
        <AdvancedAnalysisPage />
      </MemoryRouter>
    );

    expect(await screen.findByText(/blocked by the data-readiness gate/i)).toBeInTheDocument();
    expect(screen.getByTestId("phase8-readiness-state-badge")).toHaveTextContent(/blocked/i);
    expect(screen.getByTestId("phase8-readiness-gates")).toBeInTheDocument();

    expect(screen.getAllByText(/n below threshold/i).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/not enough independent heats/i).length).toBeGreaterThan(0);
  });

  it("renders ranked contributors with method + honesty caveat", async () => {
    vi.mocked(getAnalysisReadiness).mockResolvedValue(rd("Ready", true) as never);
    vi.mocked(getAnalysisReadinessGates).mockResolvedValue(gates("Ready") as never);
    vi.mocked(getAdvancedResults).mockResolvedValue([
      {
        findingId: "casting_speed",
        method: "Spearman",
        effectSize: 0.6,
        qValue: 0.01,
        sampleSize: 90,
        stabilityLower: 0.4,
        stabilityUpper: 0.8,
        stabilityConsistency: 0.98,
        isStable: true,
        survivesStratification: true,
        significant: true,
        isRenderable: true,
        honestyCaveat: "This is a diagnostic association, not an evidence-backed root-cause investigation.",
      },
    ] as never);

    render(
      <MemoryRouter initialEntries={["/investigate/advanced?outcomeKey=defect.edge_crack_rate"]}>
        <AdvancedAnalysisPage />
      </MemoryRouter>
    );

    expect(await screen.findByText(/Spearman/i)).toBeInTheDocument();
    expect(screen.getByText(/diagnostic association/i)).toBeInTheDocument();
    expect(screen.getByText(/casting_speed/i)).toBeInTheDocument();
  });
});