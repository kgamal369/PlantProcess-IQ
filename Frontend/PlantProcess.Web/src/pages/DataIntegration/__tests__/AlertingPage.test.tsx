import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi, beforeEach } from "vitest";

const listRulesMock = vi.fn();
const listLogMock = vi.fn();
const createRuleMock = vi.fn();
const evaluateMock = vi.fn();
vi.mock("@/api/engine/alerts.api", () => ({
  listRules: (...a: unknown[]) => listRulesMock(...a),
  listLog: (...a: unknown[]) => listLogMock(...a),
  createRule: (...a: unknown[]) => createRuleMock(...a),
  evaluateAlerts: (...a: unknown[]) => evaluateMock(...a),
}));

import { AlertingPage } from "../AlertingPage";

describe("AlertingPage (M1-06 / UI-4)", () => {
  beforeEach(() => {
    listRulesMock.mockReset().mockResolvedValue([]);
    listLogMock.mockReset().mockResolvedValue([]);
    createRuleMock.mockReset();
    evaluateMock.mockReset();
  });

  it("renders both honest empty states", async () => {
    render(<AlertingPage />);
    expect(await screen.findByText(/No rules yet/i)).toBeTruthy();
    expect(await screen.findByText(/No breaches logged yet/i)).toBeTruthy();
  });

  it("client validation: required fields and numeric limit", async () => {
    render(<AlertingPage />);
    await screen.findByText(/No rules yet/i);

    await userEvent.click(screen.getByRole("button", { name: /Add rule/i }));
    expect(await screen.findByText(/Rule name and parameter code are required/i)).toBeTruthy();
    expect(createRuleMock).not.toHaveBeenCalled();

    await userEvent.type(screen.getByPlaceholderText("Superheat high"), "R1");
    await userEvent.type(screen.getByPlaceholderText("SUPERHEAT_C"), "P1");
    await userEvent.type(screen.getByPlaceholderText("36"), "abc");
    await userEvent.click(screen.getByRole("button", { name: /Add rule/i }));
    expect(await screen.findByText(/Limit must be a number/i)).toBeTruthy();
    expect(createRuleMock).not.toHaveBeenCalled();
  });

  it("valid submit calls createRule with the exact body", async () => {
    createRuleMock.mockResolvedValue({ id: "x" });
    render(<AlertingPage />);
    await screen.findByText(/No rules yet/i);

    await userEvent.type(screen.getByPlaceholderText("Superheat high"), "Superheat high");
    await userEvent.type(screen.getByPlaceholderText("SUPERHEAT_C"), "SUPERHEAT_C");
    await userEvent.type(screen.getByPlaceholderText("36"), "36");
    await userEvent.click(screen.getByRole("button", { name: /Add rule/i }));

    await waitFor(() => expect(createRuleMock).toHaveBeenCalledWith({
      ruleName: "Superheat high",
      parameterCode: "SUPERHEAT_C",
      comparator: ">",
      limitValue: 36,
      severity: "Warning",
    }));
    expect(await screen.findByText(/Rule created/i)).toBeTruthy();
  });
});