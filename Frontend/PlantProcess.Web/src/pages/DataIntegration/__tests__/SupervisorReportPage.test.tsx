import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi, beforeEach } from "vitest";

const listMock = vi.fn();
const runMock = vi.fn();
vi.mock("@/api/engine/supervisor.api", () => ({
  listSupervisorReports: (...a: unknown[]) => listMock(...a),
  runSupervisor: (...a: unknown[]) => runMock(...a),
}));

import { SupervisorReportPage } from "../SupervisorReportPage";

describe("SupervisorReportPage (M1-05)", () => {
  beforeEach(() => { listMock.mockReset(); runMock.mockReset(); });

  it("shows the honest empty state when no reports exist", async () => {
    listMock.mockResolvedValue([]);
    render(<SupervisorReportPage />);
    expect(await screen.findByText(/No supervisor reports yet/i)).toBeTruthy();
  });

  it("Run review now calls the API and renders the new report", async () => {
    listMock.mockResolvedValueOnce([]);
    runMock.mockResolvedValue({ id: "r1", itemKey: "k1", title: "Supervisor report X", body: "b", findings: 1, significant: 0 });
    listMock.mockResolvedValueOnce([{ id: "r1", itemKey: "k1", title: "Supervisor report X", body: "NOTE (v0): read-only", createdAtUtc: "t" }]);

    render(<SupervisorReportPage />);
    await screen.findByText(/No supervisor reports yet/i);
    await userEvent.click(screen.getByRole("button", { name: /Run review now/i }));

    await waitFor(() => expect(runMock).toHaveBeenCalledTimes(1));
    expect(await screen.findByText("Supervisor report X")).toBeTruthy();
  });
});