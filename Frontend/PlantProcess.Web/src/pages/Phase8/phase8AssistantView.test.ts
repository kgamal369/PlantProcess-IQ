
import { describe, expect, it } from "vitest";
import { assistantModeLabel, formatEuroRange, type Phase8AssistantConfiguration } from "@/api/phase8Assistant";

describe("Phase 8 assistant HMI helpers", () => {
  it("formats bounded recommendation euro ranges", () => {
    const text = formatEuroRange({
      expectedValueLow: 28000,
      expectedValueExpected: 42000,
      expectedValueHigh: 56000,
      currencyCode: "EUR",
    });

    expect(text).toContain("28");
    expect(text).toContain("56");
    expect(text).toMatch(/expected/i);
  });

  it("labels no-egress assistant mode", () => {
    const config: Phase8AssistantConfiguration = {
      mode: "grounded-extractive",
      groundingPolicy: "strict-citations-required",
      evidencePolicy: "citations-and-provenance-required",
      noEgress: true,
      maxCitations: 5,
      allowedTools: ["material-investigation"],
      requireHumanApprovalForRecommendations: true,
      enableSuggestionWorkflow: true,
      updatedBy: "test",
      updatedAtUtc: new Date().toISOString(),
    };

    expect(assistantModeLabel(config)).toContain("no-egress");
  });
});
