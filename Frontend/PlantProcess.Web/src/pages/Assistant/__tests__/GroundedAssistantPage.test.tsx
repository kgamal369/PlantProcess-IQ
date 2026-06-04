import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { GroundedAssistantPage } from "../GroundedAssistantPage";

describe("GroundedAssistantPage", () => {
  it("shows citation and abstention affordances", () => {
    render(<GroundedAssistantPage />);

    expect(screen.getByText(/Grounded Assistant/i)).toBeInTheDocument();
    expect(screen.getByText(/must cite retrieved evidence/i)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /Ask assistant/i })).toBeInTheDocument();
  });
});