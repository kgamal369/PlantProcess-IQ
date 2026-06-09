import { describe, expect, it } from "vitest";
import { canAccess, executiveDashboardCards, personaFor, routeDecision, visibleNavigation } from "./phase9RoleAccess";

describe("Phase 9 role/access and persona visibility", () => {
  it("allows executive dashboard but blocks admin configuration for executive", () => {
    expect(canAccess("Executive", "Enterprise", "ExecutiveDashboardView")).toBe(true);
    expect(canAccess("Executive", "Enterprise", "AdminUserManagement")).toBe(false);
  });

  it("returns disabled-with-reason instead of a dead route", () => {
    const decision = routeDecision("/phase8/assistant-config", "MaintenanceEngineer", "Enterprise");

    expect(decision.allowed).toBe(false);
    expect(decision.disabled).toBe(true);
    expect(decision.reason).toMatch(/does not grant/i);
  });

  it("maps CEO to executive persona", () => {
    expect(personaFor("ChiefExecutiveOfficer")).toBe("executive");
  });

  it("builds executive decision dashboard cards", () => {
    const cards = executiveDashboardCards();

    expect(cards.some((x) => x.title.includes("Value"))).toBe(true);
    expect(cards.some((x) => x.title.includes("Downtime"))).toBe(true);
    expect(cards.every((x) => x.caveat.length > 0)).toBe(true);
  });

  it("shows disabled navigation entries with reasons", () => {
    const nav = visibleNavigation("Viewer", "Starter");
    const disabled = nav.filter((x) => x.disabled);

    expect(disabled.length).toBeGreaterThan(0);
    expect(disabled.every((x) => x.reason.length > 0)).toBe(true);
  });
});