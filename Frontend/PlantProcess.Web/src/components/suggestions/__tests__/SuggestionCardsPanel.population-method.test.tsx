import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { SuggestionCardsPanel, type SuggestionCard } from "../SuggestionCardsPanel";

const card: SuggestionCard = {
  id: "00000000-0000-0000-0000-000000000abc",
  title: "Review suspected association on param_pressure",
  actionType: "ReviewAssociation",
  status: "open",
  confidence: 0.71,
  population: 142,
  method: "Spearman",
  impactLow: 10000,
  impactHigh: 25000,
  honestyText: "Estimated range, not a promise.",
  evidence: [{ kind: "Finding", id: "finding-CM" }],
  sourceFindings: ["finding-CM"],
};

describe("SuggestionCardsPanel population/method", () => {
  it("renders population and method alongside the euro range", () => {
    render(<SuggestionCardsPanel cards={[card]} />);
    expect(screen.getByTestId("suggestion-cards")).toBeInTheDocument();
    expect(screen.getByTestId("suggestion-population")).toHaveTextContent("Population 142");
    expect(screen.getByTestId("suggestion-method")).toHaveTextContent("Spearman");
  });

  it("omits population/method when absent (no zero, no empty method)", () => {
    const bare: SuggestionCard = { ...card, population: 0, method: "" };
    render(<SuggestionCardsPanel cards={[bare]} />);
    expect(screen.queryByTestId("suggestion-population")).toBeNull();
    expect(screen.queryByTestId("suggestion-method")).toBeNull();
  });
});