import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { MetricCard } from "../MetricCard";

describe("MetricCard", () => {
  it("renders title and value", () => {
    render(
      <MetricCard
        title="Risk Score"
        value="82"
      />
    );

    expect(screen.getByText("Risk Score")).toBeInTheDocument();
    expect(screen.getByText("82")).toBeInTheDocument();
  });

  it("renders optional subtitle", () => {
    render(
      <MetricCard
        title="Data Quality"
        value="94%"
        subtitle="Ready for demo"
      />
    );

    expect(screen.getByText("Data Quality")).toBeInTheDocument();
    expect(screen.getByText("94%")).toBeInTheDocument();
    expect(screen.getByText("Ready for demo")).toBeInTheDocument();
  });

  it("renders optional icon", () => {
    render(
      <MetricCard
        title="High Risk"
        value={12}
        icon={<span data-testid="metric-icon">!</span>}
      />
    );

    expect(screen.getByText("High Risk")).toBeInTheDocument();
    expect(screen.getByText("12")).toBeInTheDocument();
    expect(screen.getByTestId("metric-icon")).toBeInTheDocument();
  });
});