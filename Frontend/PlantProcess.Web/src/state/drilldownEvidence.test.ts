import { describe, expect, it, vi } from "vitest";
import {
  describePopulationCount, executionEvidenceWarning, resolveExecutionEvidence,
} from "./drilldownEvidence";

const HANDLE = { kind: "WidgetResult", id: "e-1" };

describe("T-050 drilldown evidence", () => {
  it("surfaces the server's execution_evidence_unavailable warning verbatim", async () => {
    const warning =
      "execution_evidence_unavailable: execution evidence was requested but the " +
      "execution identity is incomplete.";

    const resolver = vi.fn();
    const result = await resolveExecutionEvidence(HANDLE, [warning], resolver);

    expect(result).toEqual({ status: "unavailable", reason: warning });
    // The warning is decisive: nothing is asked of the resolver.
    expect(resolver).not.toHaveBeenCalled();
  });

  it("passes the handle id to the existing resolver, unchanged", async () => {
    const evidence = { evidenceId: "e-1", available: true };
    const resolver = vi.fn().mockResolvedValue(evidence);

    const result = await resolveExecutionEvidence(HANDLE, [], resolver);

    expect(resolver).toHaveBeenCalledTimes(1);
    expect(resolver).toHaveBeenCalledWith("e-1");
    expect(result).toEqual({ status: "resolved", evidence });
  });

  it("treats a null resolver result as not found, not as a failure", async () => {
    // The client turns 404 into null on purpose: evidence not available to this
    // tenant is a different thing from the request failing.
    const result = await resolveExecutionEvidence(HANDLE, [], vi.fn().mockResolvedValue(null));
    expect(result).toEqual({ status: "notFound" });
  });

  it("keeps a transport failure distinguishable from an absence of evidence", async () => {
    const result = await resolveExecutionEvidence(
      HANDLE, [], vi.fn().mockRejectedValue(new Error("evidence request failed: 503")),
    );

    expect(result.status).toBe("error");
    expect(result.status === "error" && result.message).toContain("503");
  });

  it("reports an execution that offered no handle as unavailable, and asks nothing", async () => {
    const resolver = vi.fn();

    expect((await resolveExecutionEvidence(null, [], resolver)).status).toBe("unavailable");
    expect((await resolveExecutionEvidence({ kind: "WidgetResult", id: "" }, [], resolver)).status)
      .toBe("unavailable");
    expect(resolver).not.toHaveBeenCalled();
  });

  it("finds the warning among unrelated ones and ignores an empty set", () => {
    expect(executionEvidenceWarning(["something else", "execution_evidence_unavailable: x"]))
      .toBe("execution_evidence_unavailable: x");
    expect(executionEvidenceWarning([])).toBeNull();
    expect(executionEvidenceWarning(null)).toBeNull();
  });

  it("never substitutes a number for an unknown population count", () => {
    expect(describePopulationCount(null)).toBe("not reported by this source");
    expect(describePopulationCount(undefined)).toBe("not reported by this source");
    expect(describePopulationCount(0)).toBe("0");
    expect(describePopulationCount(1420)).toBe("1420");
  });
});
