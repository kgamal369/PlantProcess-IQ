import { describe, expect, it } from "vitest";

import { displayValue } from "../AssociativePanel";

/**
 * DEMO-010. The associative strip rendered raw dimension values, so the
 * EQUIPMENT column showed ten 36-character identifiers. Measured against
 * ppiq_presentation on 19 Aug 2026: public.equipment holds 18 of 18 rows with
 * both equipment_code and equipment_name, and the identifiers on screen map
 * exactly onto them - 7922750e-2768-5083-9cc3-cc0ab890b32b is HSM-01,
 * "Hot strip mill".
 *
 * No name is invented here. Until the dimension enumeration carries a governed
 * label, an identifier is shortened for display and the full value stays in the
 * chip's title, so nothing is hidden and nothing is fabricated.
 */
describe("DEMO-010 associative chip labels", () => {
  it("shortens a raw identifier instead of showing 36 characters", () => {
    const shown = displayValue("7922750e-2768-5083-9cc3-cc0ab890b32b");

    expect(shown).toBe("7922750e\u2026");
    expect(shown.length).toBeLessThan(12);
  });

  it("never alters a value that is already human readable", () => {
    expect(displayValue("HSM-01")).toBe("HSM-01");
    expect(displayValue("Hot strip mill")).toBe("Hot strip mill");
    expect(displayValue("Coil")).toBe("Coil");
    expect(displayValue("CASTING_SPEED_MPM")).toBe("CASTING_SPEED_MPM");
    expect(displayValue("unknown")).toBe("unknown");
  });

  it("leaves values that merely resemble an identifier untouched", () => {
    expect(displayValue("7922750e-2768-5083-9cc3")).toBe("7922750e-2768-5083-9cc3");
    expect(displayValue("FLEET_V2")).toBe("FLEET_V2");
    expect(displayValue("")).toBe("");
  });
});
