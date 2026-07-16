import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";

const listBatches = vi.fn();
const createMapping = vi.fn();
const executeMapping = vi.fn();
const listRules = vi.fn();
const listLog = vi.fn();
const createRule = vi.fn();
const evaluateAlerts = vi.fn();
const listReports = vi.fn();
const runSupervisor = vi.fn();

vi.mock("@/api/integration/mappingAuthor.api", () => ({
  listImportBatches: (...args: unknown[]) => listBatches(...args),
  createMappingDefinition: (...args: unknown[]) => createMapping(...args),
  executeMapping: (...args: unknown[]) => executeMapping(...args),
}));

vi.mock("@/api/engine/alerts.api", () => ({
  listRules: (...args: unknown[]) => listRules(...args),
  listLog: (...args: unknown[]) => listLog(...args),
  createRule: (...args: unknown[]) => createRule(...args),
  evaluateAlerts: (...args: unknown[]) => evaluateAlerts(...args),
}));

vi.mock("@/api/engine/supervisor.api", () => ({
  listSupervisorReports: (...args: unknown[]) => listReports(...args),
  runSupervisor: (...args: unknown[]) => runSupervisor(...args),
}));

import { AuthorMappingPage } from "../AuthorMappingPage";
import { AlertingPage } from "../AlertingPage";
import { SupervisorReportPage } from "../SupervisorReportPage";

const batch = {
  id: "batch-1",
  sourceSystemDefinitionId: "source-definition-1",
  sourceObjectName: "parameter_definitions",
  sourceSystem: "postgresql",
  status: "Completed",
  startedAtUtc: "2026-07-15T10:00:00Z",
};

describe("Canonical journey critical frontend surfaces", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    listBatches.mockResolvedValue([batch]);
    listRules.mockResolvedValue([]);
    listLog.mockResolvedValue([]);
    listReports.mockResolvedValue([]);
  });

  it("J04 authors a real MappingDefinition payload including const literals", async () => {
    createMapping.mockResolvedValue({ id: "mapping-1" });
    render(<AuthorMappingPage />);

    const target = await screen.findByLabelText("Target field 1");
    const source = screen.getByLabelText("Source field 1");
    fireEvent.change(target, { target: { value: "ParameterCode" } });
    fireEvent.change(source, { target: { value: "const:SUPERHEAT_C" } });

    await userEvent.click(screen.getByRole("button", { name: /Save mapping/i }));

    await waitFor(() => expect(createMapping).toHaveBeenCalledTimes(1));
    const payload = createMapping.mock.calls[0][0] as { mappingJson: string; targetEntityName: string };
    expect(payload.targetEntityName).toBe("DefectCatalog");
    expect(JSON.parse(payload.mappingJson)).toEqual({ ParameterCode: "const:SUPERHEAT_C" });
    expect(await screen.findByText(/Mapping saved/i)).toBeVisible();
  });

  it("J04 executes a saved mapping and folds technical response details", async () => {
    createMapping.mockResolvedValue({ id: "mapping-1" });
    executeMapping.mockResolvedValue({ mappedRows: 7, failedRows: 1, processed: 8 });
    render(<AuthorMappingPage />);

    fireEvent.change(await screen.findByLabelText("Target field 1"), { target: { value: "DefectCode" } });
    fireEvent.change(screen.getByLabelText("Source field 1"), { target: { value: "defect_code" } });
    await userEvent.click(screen.getByRole("button", { name: /Save mapping/i }));
    await waitFor(() => expect(createMapping).toHaveBeenCalled());
    await userEvent.click(screen.getByRole("button", { name: /Execute projection/i }));

    expect(await screen.findByText("Mapped: 7")).toBeVisible();
    expect(screen.getByText("Failed: 1")).toBeVisible();
    expect(screen.getByText(/Technical response details/i)).toBeVisible();
  });

  it("UI4 validates, creates and evaluates an alert rule", async () => {
    createRule.mockResolvedValue({ id: "rule-1" });
    evaluateAlerts.mockResolvedValue({ logged: 2 });
    render(<AlertingPage />);

    await screen.findByText(/No rules yet/i);
    await userEvent.click(screen.getByRole("button", { name: /Add rule/i }));
    expect(await screen.findByText(/Rule name and parameter code are required/i)).toBeVisible();

    await userEvent.type(screen.getByPlaceholderText("Superheat high"), "Superheat high");
    await userEvent.type(screen.getByPlaceholderText("SUPERHEAT_C"), "SUPERHEAT_C");
    await userEvent.type(screen.getByPlaceholderText("36"), "36");
    await userEvent.click(screen.getByRole("button", { name: /Add rule/i }));
    await waitFor(() => expect(createRule).toHaveBeenCalledWith({
      ruleName: "Superheat high",
      parameterCode: "SUPERHEAT_C",
      comparator: ">",
      limitValue: 36,
      severity: "Warning",
    }));

    await userEvent.click(screen.getByRole("button", { name: /Run evaluation/i }));
    expect(await screen.findByText(/Evaluation complete: 2/i)).toBeVisible();
  });

  it("J14 keeps the newest supervisor report open and older reports folded", async () => {
    listReports.mockResolvedValue([
      { id: "r2", itemKey: "k2", title: "Supervisor report latest", body: "Latest body", createdAtUtc: "2026-07-15" },
      { id: "r1", itemKey: "k1", title: "Supervisor report earlier", body: "Earlier body", createdAtUtc: "2026-07-14" },
    ]);
    render(<SupervisorReportPage />);

    expect(await screen.findByText("Supervisor report latest")).toBeVisible();
    const details = document.querySelectorAll(".ppiq-sup-item details");
    expect(details).toHaveLength(2);
    expect(details[0]).toHaveAttribute("open");
    expect(details[1]).not.toHaveAttribute("open");
  });
});
