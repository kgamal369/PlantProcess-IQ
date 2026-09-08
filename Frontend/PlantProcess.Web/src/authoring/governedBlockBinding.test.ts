import { describe, expect, it } from "vitest";
import { blockById, contractForKind, seedForKind } from "./blockRegistry";
import { parameterValueProblem } from "./blockParameters";
import { bindGovernedAggregate, bindGovernedWindow } from "./governedBlockBinding";

describe("governed aggregate binding", () => {
  it("copies canonical aggregation semantics exactly and never substitutes Average", () => {
    const result = bindGovernedAggregate({
      measures: [{
        code: "customer.energy_delta",
        aggregation: "Delta",
        signalCode: "energy.counter",
      }],
      refusals: [],
    }, "customer.energy_delta");

    expect(result).toEqual({
      ok: true,
      data: {
        measureCode: "customer.energy_delta",
        aggregation: "Delta",
        signalCode: "energy.counter",
      },
    });
    expect(JSON.stringify(result)).not.toContain("Average");
  });

  it("refuses AG01 when canonical metadata publishes no aggregation semantics", () => {
    const result = bindGovernedAggregate({
      measures: [{ code: "customer.measure_without_semantics", aggregation: null }],
      refusals: [{
        code: "customer.measure_without_semantics",
        errorCode: "AG01",
        message: "aggregation_semantics_undeclared",
      }],
    }, "customer.measure_without_semantics");

    expect(result).toEqual({
      ok: false,
      refusal: {
        code: "AG01",
        message: "aggregation_semantics_undeclared",
      },
    });
  });

  it("refuses an unpublished measure rather than manufacturing a local definition", () => {
    const result = bindGovernedAggregate({ measures: [], refusals: [] }, "not.published");
    expect(result.ok).toBe(false);
    if (!result.ok) {
      expect(result.refusal.code).toBe("MEASURE_AUTHORITY_UNDECLARED");
    }
  });
});

describe("governed window binding", () => {
  it("binds exactly one declared window and does not invent a default", () => {
    expect(bindGovernedWindow([{ code: "rolling_30d" }], "rolling_30d"))
      .toEqual({ ok: true, data: { windowCode: "rolling_30d" } });

    const missing = bindGovernedWindow([{ code: "rolling_30d" }], "");
    expect(missing.ok).toBe(false);
    if (!missing.ok) {
      expect(missing.refusal.code).toBe("WINDOW_AUTHORITY_UNDECLARED");
    }
  });
});

describe("aggregate and window catalogue closure", () => {
  it("both families are implemented validation contracts but remain non-persistable until Canvas persistence", () => {
    for (const kind of ["aggregate", "window"] as const) {
      const block = blockById(kind);
      expect(block?.implemented, kind).toBe(true);
      expect(block?.capabilities.evaluable, kind).toBe(false);
      expect(block?.capabilities.persistable, kind).toBe(false);
      expect(seedForKind(kind), kind).toEqual({});
      expect(contractForKind(kind), kind).not.toBeNull();
    }
  });

  it("aggregate and window require explicit governed bindings", () => {
    const aggregate = contractForKind("aggregate");
    const window = contractForKind("window");
    if (!aggregate || !window) { throw new Error("governed family contract missing"); }

    expect(parameterValueProblem(aggregate.parameters[0], "")).toContain("governed measure");
    expect(parameterValueProblem(aggregate.parameters[1], "")).toContain("AG01");
    expect(parameterValueProblem(window.parameters[0], "")).toContain("no governed window binding");

    expect(parameterValueProblem(aggregate.parameters[0], "customer.measure")).toBeNull();
    expect(parameterValueProblem(aggregate.parameters[1], "Delta")).toBeNull();
    expect(parameterValueProblem(window.parameters[0], "rolling_30d")).toBeNull();
  });
});
