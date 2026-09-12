// PPIQ T-262. THE OUTPUT MAPPING SURFACE.
//
// The subject is the difference between a SUGGESTION and a DECISION. Everything else
// here exists to prove that nothing is bound unless a person bound it.

import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";

const listOutputTargetFields = vi.fn();

vi.mock("@/api/canvasProjectionApi", () => ({
  listOutputTargetFields: (...a: unknown[]) => listOutputTargetFields(...a),
}));

const { OutputMappingInspector } = await import("./OutputMappingInspector");

const FIELDS = {
  entity: "QualityEvent",
  source: "canonical-model",
  fields: [
    { name: "Severity", clrType: "Int32", isRequired: true, isSystemOwned: false, isAuthorWritable: true },
    { name: "Comment", clrType: "String", isRequired: false, isSystemOwned: false, isAuthorWritable: true },
    { name: "Id", clrType: "Guid", isRequired: true, isSystemOwned: true, isAuthorWritable: false },
    { name: "SourceRecordId", clrType: "String", isRequired: false, isSystemOwned: true, isAuthorWritable: false },
  ],
};

const OUTPUTS = [
  { kind: "column" as const, table: "defects", name: "Severity" },
  { kind: "column" as const, table: "defects", name: "note" },
  { kind: "derived" as const, name: "ratio" },
];

beforeEach(() => {
  listOutputTargetFields.mockReset();
  listOutputTargetFields.mockResolvedValue(FIELDS);
});

describe("mapping authored outputs onto canonical fields", () => {
  it("asks the catalogue for the target's fields and compiles none of its own", async () => {
    render(
      <OutputMappingInspector
        outputTarget="QualityEvent" outputs={OUTPUTS} declaration={null} onChange={() => {}} />);

    await waitFor(() => expect(listOutputTargetFields).toHaveBeenCalledWith("QualityEvent"));
    expect(await screen.findByTestId("map-Severity")).toBeTruthy();
    expect(screen.getByTestId("map-Comment")).toBeTruthy();
  });

  it("never offers a platform-owned field, and says why it is absent", async () => {
    render(
      <OutputMappingInspector
        outputTarget="QualityEvent" outputs={OUTPUTS} declaration={null} onChange={() => {}} />);

    await screen.findByTestId("map-Severity");

    // Identity and provenance are runtime invariants. The server refuses them too;
    // this is the courtesy, not the rule.
    expect(screen.queryByTestId("map-Id")).toBeNull();
    expect(screen.queryByTestId("map-SourceRecordId")).toBeNull();
    expect(screen.getByTestId("output-mapping-system-owned").textContent).toContain("2 field(s)");
  });

  it("offers an exact same-name match as a suggestion and binds NOTHING until it is taken",
    async () => {
      const onChange = vi.fn();
      const user = userEvent.setup();

      render(
        <OutputMappingInspector
          outputTarget="QualityEvent" outputs={OUTPUTS} declaration={null} onChange={onChange} />);

      // Severity matches an output called Severity exactly, so the suggestion appears.
      const suggest = await screen.findByTestId("map-suggest-Severity");

      // The decisive assertion: showing it changed nothing.
      expect(onChange).not.toHaveBeenCalled();

      await user.click(suggest);

      expect(onChange).toHaveBeenCalledWith({
        targetEntity: "QualityEvent",
        fieldBindings: [
          { targetField: "Severity", sourceKind: "column", sourceTable: "defects", sourceField: "Severity" },
        ],
      });
    });

  it("offers no suggestion where no name matches exactly", async () => {
    render(
      <OutputMappingInspector
        outputTarget="QualityEvent" outputs={OUTPUTS} declaration={null} onChange={() => {}} />);

    await screen.findByTestId("map-Comment");

    // "note" and "ratio" are not "Comment". Nothing looser than equality is offered.
    expect(screen.queryByTestId("map-suggest-Comment")).toBeNull();
  });

  it("binds a derived output the author chooses explicitly", async () => {
    const onChange = vi.fn();
    const user = userEvent.setup();

    render(
      <OutputMappingInspector
        outputTarget="QualityEvent" outputs={OUTPUTS} declaration={null} onChange={onChange} />);

    await screen.findByTestId("map-select-Comment");
    await user.selectOptions(screen.getByTestId("map-select-Comment"), "derived||ratio");

    expect(onChange).toHaveBeenCalledWith({
      targetEntity: "QualityEvent",
      fieldBindings: [
        { targetField: "Comment", sourceKind: "derived", sourceTable: null, sourceField: "ratio" },
      ],
    });
  });

  it("removes a binding when the author chooses nothing", async () => {
    const onChange = vi.fn();
    const user = userEvent.setup();

    render(
      <OutputMappingInspector
        outputTarget="QualityEvent"
        outputs={OUTPUTS}
        declaration={{
          targetEntity: "QualityEvent",
          fieldBindings: [
            { targetField: "Severity", sourceKind: "column", sourceTable: "defects", sourceField: "Severity" },
          ],
        }}
        onChange={onChange} />);

    await screen.findByTestId("map-select-Severity");
    await user.selectOptions(screen.getByTestId("map-select-Severity"), "");

    // Nothing left, so there is no declaration - not a declaration with an empty list.
    expect(onChange).toHaveBeenCalledWith(null);
  });

  it("names the required fields that are still unmapped", async () => {
    render(
      <OutputMappingInspector
        outputTarget="QualityEvent" outputs={OUTPUTS} declaration={null} onChange={() => {}} />);

    const missing = await screen.findByTestId("output-mapping-missing");
    expect(missing.textContent).toContain("Severity");
    expect(missing.textContent).not.toContain("Comment");
  });

  it("states that a legacy version declares nothing, and fabricates no mapping", async () => {
    render(
      <OutputMappingInspector
        outputTarget="QualityEvent" outputs={OUTPUTS} declaration={null} onChange={() => {}}
        legacyWithoutDeclaration />);

    expect((await screen.findByTestId("output-mapping-legacy")).textContent)
      .toContain("declares none");
    expect(screen.queryByTestId("map-suggest-Severity")).toBeTruthy();
  });

  it("shows the server's refusal in its own words", async () => {
    render(
      <OutputMappingInspector
        outputTarget="QualityEvent" outputs={OUTPUTS} declaration={null} onChange={() => {}}
        refusal="QualityEvent.Severity is required and nothing is bound to it." />);

    expect((await screen.findByTestId("output-mapping-refusal")).textContent)
      .toContain("is required and nothing is bound to it");
  });

  it("says the fields could not be read rather than showing an empty target", async () => {
    listOutputTargetFields.mockRejectedValue(new Error("unreachable"));

    render(
      <OutputMappingInspector
        outputTarget="QualityEvent" outputs={OUTPUTS} declaration={null} onChange={() => {}} />);

    expect((await screen.findByTestId("output-mapping-load-failure")).textContent)
      .toContain("could not be read");
    expect(screen.queryByTestId("map-Severity")).toBeNull();
  });

  it("asks for nothing until a target is chosen", async () => {
    render(
      <OutputMappingInspector outputTarget="" outputs={OUTPUTS} declaration={null} onChange={() => {}} />);

    await waitFor(() => expect(listOutputTargetFields).not.toHaveBeenCalled());
    expect(screen.getByTestId("output-mapping-target").textContent)
      .toContain("Choose a governed output target");
  });
});