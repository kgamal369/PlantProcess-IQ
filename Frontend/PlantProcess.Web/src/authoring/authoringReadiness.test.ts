import { describe, expect, it } from "vitest";

import { authoringReadiness, readinessBlockedMessage } from "./authoringReadiness";

describe("T-242 authoring readiness", () => {
  it("C242-47 a sound board that can be saved is ready and may run", () => {
    const readiness = authoringReadiness(null, null);
    expect(readiness.state).toBe("ready");
    expect(readiness.label).toBe("Valid flow");
    expect(readiness.canRun).toBe(true);
  });

  it("C242-48 a semantic problem is reported as Invalid and names its own reason", () => {
    const readiness = authoringReadiness("Filter 1: its dataset input is not connected.", null);
    expect(readiness.state).toBe("invalid");
    expect(readiness.label).toBe("Invalid");
    expect(readiness.detail).toContain("Filter 1");
    expect(readiness.canRun).toBe(false);
  });

  it("C242-49 a board that cannot cross the save boundary is NOT reported as Invalid", () => {
    const refusal = "Arithmetic 1 (arithmetic-1, kind arithmetic) cannot be saved as a transformation.";
    const readiness = authoringReadiness(null, refusal);
    expect(readiness.state).toBe("unsavable");
    expect(readiness.label).toBe("Cannot be saved");
    expect(readiness.label).not.toBe("Invalid");
    expect(readiness.detail).toBe(refusal);
  });

  it("C242-50 a board that cannot be saved may not run either", () => {
    const readiness = authoringReadiness(null, "cannot be saved as a transformation");
    expect(readiness.canRun).toBe(false);
  });

  it("C242-51 the composite state is never reported as unconditionally valid", () => {
    for (const [invalid, refusal] of [
      ["a problem", null],
      [null, "a refusal"],
      ["a problem", "a refusal"],
    ] as Array<[string | null, string | null]>) {
      const readiness = authoringReadiness(invalid, refusal);
      expect(readiness.state).not.toBe("ready");
      expect(readiness.label).not.toBe("Valid flow");
      expect(readiness.canRun).toBe(false);
    }
  });

  it("C242-52 a semantic problem is reported before a save refusal", () => {
    // The actionable message names a block. Burying it under a structural one
    // would send the author to the wrong place.
    const readiness = authoringReadiness("Filter 1 has no input.", "cannot be saved");
    expect(readiness.state).toBe("invalid");
    expect(readiness.detail).toContain("Filter 1");
  });

  it("C242-53 the blocked message never tells the author to hunt for a marked block that does not exist", () => {
    const unsavable = authoringReadiness(null, "Arithmetic 1 cannot be saved as a transformation.");
    const message = readinessBlockedMessage(unsavable);
    expect(message).toContain("Arithmetic 1");
    expect(message.toLowerCase()).not.toContain("marked with an error");

    const invalid = authoringReadiness("Filter 1 has no input.", null);
    expect(readinessBlockedMessage(invalid)).toContain("Filter 1");
  });
});