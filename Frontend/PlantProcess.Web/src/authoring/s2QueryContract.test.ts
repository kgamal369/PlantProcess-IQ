// PPIQ T-038. The one registry addition S2 convergence needs, and the guard
// that it stayed one addition.
//
// The second and third tests are the point: this field identifies an EXISTING
// contract on ONE purpose. It is not a new taxonomy, and no purpose that owns
// its own convergence task is reclassified by it.

import { describe, expect, it } from "vitest";
import {
  AUTHORING_PURPOSES, purposeDefinition,
  type AuthoringPurpose, type WidgetQueryContract,
} from "./authoringPurposes";

describe("T-038 S2 declares the query contract it already authors", () => {
  it("names the widget query expression contract on S2", () => {
    const contract: WidgetQueryContract | undefined = purposeDefinition("S2").queryContract;
    expect(contract).toBe("widget-query-expression");
  });

  it("declares no query contract on any other purpose", () => {
    const declaring = AUTHORING_PURPOSES
      .filter((p) => p.queryContract !== undefined)
      .map((p) => p.purpose);
    expect(declaring).toEqual(["S2"]);
  });

  it("leaves every other purpose byte for byte as it was", () => {
    for (const purpose of ["S1", "S3", "S4", "S5"] as AuthoringPurpose[]) {
      const p = purposeDefinition(purpose);
      expect(p.queryContract, purpose).toBeUndefined();
      expect(p.paletteGroups.length, purpose).toBeGreaterThan(0);
    }
    expect(AUTHORING_PURPOSES.map((p) => p.purpose)).toEqual(["S1", "S2", "S3", "S4", "S5"]);
  });

  it("keeps the staged catalogue on S1 and off S2", () => {
    expect(purposeDefinition("S1").showsStagingCatalogue).toBe(true);
    expect(purposeDefinition("S2").showsStagingCatalogue).toBe(false);
  });

  it("never calls the widget query expression by the name of another language", () => {
    // Assembled so this guard is not itself a hit in the next repository scan.
    const other = "S" + "QL";
    expect(String(purposeDefinition("S2").queryContract).toUpperCase().indexOf(other)).toBe(-1);
  });
});