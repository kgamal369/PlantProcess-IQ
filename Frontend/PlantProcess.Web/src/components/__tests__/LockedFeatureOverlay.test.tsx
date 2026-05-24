import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { LockedFeatureOverlay } from "@/components/demo/LockedFeatureOverlay";

describe("LockedFeatureOverlay", () => {
  it("shows required plan and feature name", () => {
    render(
      <LockedFeatureOverlay
        featureName="ML Learning Jobs"
        requiredPlan="Pro Plus"
      />
    );

    expect(screen.getByText(/ML Learning Jobs is locked/i)).toBeInTheDocument();
    expect(screen.getByText(/Pro Plus/i)).toBeInTheDocument();
  });
});