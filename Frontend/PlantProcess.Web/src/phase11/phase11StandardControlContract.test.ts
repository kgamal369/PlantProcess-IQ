import { describe, expect, it } from "vitest";
import {
  isNativeControlAllowedInNewCode,
  phase11StandardControlRules,
  standardComponentFor,
} from "./phase11StandardControlContract";

describe("Phase 11 standard control contract", () => {
  it("T-064 maps native controls to standard components", () => {
    expect(standardComponentFor("button")).toBe("StandardButton");
    expect(standardComponentFor("table")).toBe("StandardTable");
    expect(standardComponentFor("input")).toBe("StandardField");
    expect(standardComponentFor("select")).toBe("StandardSelect");
    expect(standardComponentFor("textarea")).toBe("StandardTextarea");
    expect(standardComponentFor("tabs")).toBe("StandardTabs");
  });

  it("T-064 requires every rule", () => {
    expect(phase11StandardControlRules.every((rule) => rule.required)).toBe(true);
    expect(phase11StandardControlRules.every((rule) => rule.reason.length > 10)).toBe(true);
  });

  it("T-064 allows native controls only in standard/generated/test boundaries", () => {
    expect(isNativeControlAllowedInNewCode("src/components/standard/StandardButton.tsx")).toBe(true);
    expect(isNativeControlAllowedInNewCode("src/example.generated.tsx")).toBe(true);
    expect(isNativeControlAllowedInNewCode("src/pages/MaterialInvestigationPage.tsx")).toBe(false);
  });
});