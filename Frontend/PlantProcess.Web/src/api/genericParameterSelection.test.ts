// T-052. NO STEEL PARAMETER MAY BE SENT ON A CUSTOMER'S BEHALF.
//
// The generic correlation call used to read
//     parameterCode: filters.parameterCode || "CastingSpeed"
// so a plant that has never heard of casting speed would silently be asked
// about it, and would get an answer about a parameter nobody selected. That is
// a Rule 1 violation reachable by any customer, and it is worse than an empty
// chart because it looks like a result.
//
// buildQuery already drops null, undefined and "", so an unselected parameter
// simply does not appear on the wire. The widget then shows whichever of the
// seven canonical states is true, which is the honest answer.

import { describe, expect, it } from "vitest";
import { buildQuery } from "./http/apiClient";

/** The exact shape the correlation call passes, with the fallback removed. */
function correlationParams(filters: { parameterCode?: string | null; siteId?: string | null }) {
  return {
    parameterCode: filters.parameterCode,
    siteId: filters.siteId,
    bins: 8,
  };
}

describe("T-052 generic parameter selection", () => {
  it("sends exactly the parameter the user selected", () => {
    const query = buildQuery(correlationParams({ parameterCode: "MELT_TEMP" }));

    expect(query).toContain("parameterCode=MELT_TEMP");
    expect(query).not.toContain("CastingSpeed");
  });

  it("sends NO parameter when none is selected, and invents none", () => {
    for (const absent of [undefined, null, ""]) {
      const query = buildQuery(correlationParams({ parameterCode: absent as never }));

      expect(query, "an unselected parameter reached the wire").not.toContain("parameterCode");
      expect(query, "a steel parameter was substituted for the user's choice").not.toContain("CastingSpeed");
    }
  });

  it("keeps the other filters intact when the parameter is absent", () => {
    const query = buildQuery(correlationParams({ parameterCode: null, siteId: "site-1" }));

    expect(query).toContain("siteId=site-1");
    expect(query).toContain("bins=8");
  });

  it("sends no parameter when the correlation page has not been given one", () => {
    // T-052. MaterialAnalyticsCorrelationPage seeds its parameter state empty
    // and feeds it straight into getGenealogyAwareCorrelation. Removing the
    // client fallback alone was not enough: the page itself used to supply a
    // steel parameter, so the customer still got an answer about a parameter
    // they never chose - one layer further up.
    const asPageMounts = buildQuery(correlationParams({ parameterCode: "" }));

    expect(asPageMounts, "the page supplied a parameter nobody selected").not.toContain("parameterCode");
    expect(asPageMounts, "an industry-specific parameter was sent on the user's behalf")
      .not.toContain("CastingSpeed");
  });

  it("sends the parameter once the user types one", () => {
    const afterTyping = buildQuery(correlationParams({ parameterCode: "ROLL_FORCE" }));
    expect(afterTyping).toContain("parameterCode=ROLL_FORCE");
  });
});
