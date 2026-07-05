import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { describe, expect, it, vi, beforeEach } from "vitest";

import { LogPanel } from "@/components/logging/LogPanel";

const getMock = vi.fn();
vi.mock("@/api/http", () => ({
  apiClient: { get: (...args: unknown[]) => getMock(...args) },
}));

describe("LogPanel (V1-46)", () => {
  beforeEach(() => {
    getMock.mockReset();
    getMock.mockResolvedValue({
      entries: [
        { id: "1", occurredAtUtc: "2026-07-05T14:00:00Z", jobType: "Import-Stage1", jobName: "Import-Stage1 (x)", severity: "Info", message: "Started" },
        { id: "2", occurredAtUtc: "2026-07-05T14:00:01Z", jobType: "Import-Stage1", jobName: "Import-Stage1 (x)", severity: "Error", message: "Failed after 5 ms" },
      ],
    });
  });

  it("is collapsed by default and opens to show job events", async () => {
    render(<LogPanel />);
    expect(screen.queryByText(/No job events/)).toBeNull();
    fireEvent.click(screen.getByRole("button", { name: /Job Log/ }));
    await waitFor(() => expect(screen.getByText("Started")).toBeTruthy());
    expect(screen.getByText("Failed after 5 ms")).toBeTruthy();
    expect(getMock).toHaveBeenCalled();
  });

  it("passes the severity filter to the API", async () => {
    render(<LogPanel />);
    fireEvent.click(screen.getByRole("button", { name: /Job Log/ }));
    await waitFor(() => expect(getMock).toHaveBeenCalled());
    fireEvent.change(screen.getByLabelText("Severity"), { target: { value: "Error" } });
    await waitFor(() => {
      const urls = getMock.mock.calls.map((c) => String(c[0]));
      expect(urls.some((u) => u.includes("severity=Error"))).toBe(true);
    });
  });
});
